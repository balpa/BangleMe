import AVFoundation
import CoreVideo
import CoreMedia

public final class CameraSession: NSObject {
    public typealias FrameHandler = (CVPixelBuffer, CMTime) -> Void

    public let session = AVCaptureSession()
    private let videoOutput = AVCaptureVideoDataOutput()
    private let sessionQueue = DispatchQueue(label: "BangleMe.CameraSession")
    private var frameHandler: FrameHandler?

    public private(set) var focalLengthPx: Float = 1000
    public private(set) var imageSize: CGSize = CGSize(width: 1920, height: 1080)

    public func start(handler: @escaping FrameHandler) {
        self.frameHandler = handler
        sessionQueue.async { [weak self] in self?.configureAndStart() }
    }

    public func stop() {
        sessionQueue.async { [weak self] in self?.session.stopRunning() }
    }

    private func configureAndStart() {
        guard !session.isRunning else { return }
        session.beginConfiguration()
        session.sessionPreset = .hd1920x1080

        guard let device = AVCaptureDevice.default(.builtInWideAngleCamera, for: .video, position: .back),
              let input = try? AVCaptureDeviceInput(device: device),
              session.canAddInput(input) else {
            session.commitConfiguration()
            return
        }
        session.addInput(input)

        let dims = CMVideoFormatDescriptionGetDimensions(device.activeFormat.formatDescription)
        imageSize = CGSize(width: Int(dims.width), height: Int(dims.height))

        videoOutput.videoSettings = [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA]
        videoOutput.alwaysDiscardsLateVideoFrames = true
        videoOutput.setSampleBufferDelegate(self, queue: sessionQueue)
        if session.canAddOutput(videoOutput) { session.addOutput(videoOutput) }

        if let connection = videoOutput.connection(with: .video) {
            if connection.isCameraIntrinsicMatrixDeliverySupported {
                connection.isCameraIntrinsicMatrixDeliveryEnabled = true
            }
            if connection.isVideoOrientationSupported {
                connection.videoOrientation = .portrait
            }
        }

        session.commitConfiguration()
        session.startRunning()
    }
}

extension CameraSession: AVCaptureVideoDataOutputSampleBufferDelegate {
    public func captureOutput(
        _ output: AVCaptureOutput,
        didOutput sampleBuffer: CMSampleBuffer,
        from connection: AVCaptureConnection
    ) {
        guard let pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }
        let timestamp = CMSampleBufferGetPresentationTimeStamp(sampleBuffer)

        if let intrinsicsAttachment = CMGetAttachment(
            sampleBuffer,
            key: kCMSampleBufferAttachmentKey_CameraIntrinsicMatrix,
            attachmentModeOut: nil
        ) as? Data {
            intrinsicsAttachment.withUnsafeBytes { ptr in
                if let base = ptr.baseAddress {
                    let matrix = base.assumingMemoryBound(to: matrix_float3x3.self).pointee
                    self.focalLengthPx = matrix.columns.0.x
                }
            }
        }

        frameHandler?(pixelBuffer, timestamp)
    }
}
