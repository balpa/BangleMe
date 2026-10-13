#import <AVFoundation/AVFoundation.h>

// Field of view along the long side of the camera's active format, in degrees; 0 when unknown.
// AVCaptureDevice objects are shared process-wide, so this sees the format Unity's own capture
// session selected.
extern "C" float BangleMeCameraFieldOfView(bool frontFacing)
{
    AVCaptureDevicePosition position = frontFacing ? AVCaptureDevicePositionFront : AVCaptureDevicePositionBack;
    AVCaptureDevice* device = [AVCaptureDevice defaultDeviceWithDeviceType:AVCaptureDeviceTypeBuiltInWideAngleCamera
                                                                 mediaType:AVMediaTypeVideo
                                                                  position:position];
    if (device == nil) return 0;
    return device.activeFormat.videoFieldOfView;
}
