using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BangleMe
{
    // The try-on screen: design carousel, finish chips, bracelet count and fit.
    [RequireComponent(typeof(UIDocument))]
    public sealed class TryOnScreen : MonoBehaviour
    {
        const string DefaultHint = "Bileğini kameraya göster";
        const string SelectedCardClass = "design-card--selected";
        const string SelectedChipClass = "finish-chip--selected";
        const string VisibleHintClass = "hint--visible";
        const string CollapsedSheetClass = "sheet--collapsed";

        [SerializeField] WristTrackingPipeline pipeline;
        [SerializeField] BraceletStack braceletStack;
        [SerializeField] CameraFeed cameraFeed;
        [Tooltip("One thumbnail per entry of BraceletDesign.Catalog.")]
        [SerializeField] Texture2D[] designThumbnails;

        readonly List<VisualElement> designCards = new List<VisualElement>();
        readonly List<VisualElement> finishChips = new List<VisualElement>();
        UIDocument document;
        VisualElement root;
        VisualElement sheet;
        Label hint;
        Label countLabel;
        Rect appliedSafeArea;
        Vector2Int appliedScreenSize;

        void OnEnable()
        {
            document = GetComponent<UIDocument>();
            root = document.rootVisualElement.Q("root");
            sheet = root.Q("sheet");
            hint = root.Q<Label>("hint");
            countLabel = root.Q<Label>("countLabel");

            // Collapsing the sheet gives the wrist the whole screen once the bracelets are chosen.
            root.Q<Button>("sheetHandle").clicked += () => sheet.ToggleInClassList(CollapsedSheetClass);
            root.Q<Button>("cameraButton").clicked += cameraFeed.SwitchCamera;
            root.Q<Button>("clearButton").clicked += braceletStack.Clear;
            root.Q<RepeatButton>("addButton").SetAction(() => braceletStack.Add(braceletStack.LastDesign), 400, 90);
            root.Q<RepeatButton>("removeButton").SetAction(braceletStack.RemoveLast, 400, 90);

            var slider = root.Q<Slider>("loosenessSlider");
            slider.lowValue = BraceletStack.MinLooseness;
            slider.highValue = BraceletStack.MaxLooseness;
            slider.SetValueWithoutNotify(braceletStack.Looseness);
            slider.RegisterValueChangedCallback(change => braceletStack.Looseness = change.newValue);

            BuildDesignCards(root.Q<ScrollView>("designs"));
            BuildFinishChips(root.Q("finishes"));

            braceletStack.Changed += Refresh;
            Refresh();
        }

        // The stack fills itself in Awake, which may run after this component is enabled.
        void Start()
        {
            Refresh();
        }

        void OnDisable()
        {
            braceletStack.Changed -= Refresh;
        }

        void BuildDesignCards(ScrollView carousel)
        {
            carousel.Clear();
            designCards.Clear();
            for (var i = 0; i < BraceletDesign.Catalog.Length; i++)
            {
                var index = i;
                var card = new Button(() => braceletStack.Add(index));
                card.AddToClassList("design-card");

                var thumbnail = new VisualElement { pickingMode = PickingMode.Ignore };
                thumbnail.AddToClassList("design-thumb");
                if (i < designThumbnails.Length) thumbnail.style.backgroundImage = designThumbnails[i];
                card.Add(thumbnail);

                var label = new Label(BraceletDesign.Catalog[i].Name) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("design-name");
                card.Add(label);

                carousel.Add(card);
                designCards.Add(card);
            }
        }

        void BuildFinishChips(VisualElement container)
        {
            container.Clear();
            finishChips.Clear();
            for (var i = 0; i < BraceletFinish.Catalog.Length; i++)
            {
                var index = i;
                var finish = BraceletFinish.Catalog[i];
                var chip = new Button(() => braceletStack.FinishIndex = index) { tooltip = finish.Name };
                chip.AddToClassList("finish-chip");
                if (finish.SmoothnessScale < 1f) chip.AddToClassList("finish-chip--matte");
                chip.style.backgroundColor = finish.Color;
                container.Add(chip);
                finishChips.Add(chip);
            }
        }

        void Refresh()
        {
            countLabel.text = braceletStack.Count.ToString();
            for (var i = 0; i < designCards.Count; i++)
            {
                designCards[i].EnableInClassList(SelectedCardClass, braceletStack.Count > 0 && i == braceletStack.LastDesign);
            }
            for (var i = 0; i < finishChips.Count; i++)
            {
                finishChips[i].EnableInClassList(SelectedChipClass, i == braceletStack.FinishIndex);
            }
        }

        void Update()
        {
            hint.text = pipeline.Error ?? DefaultHint;
            hint.EnableInClassList(VisibleHintClass, !pipeline.IsTracking);
            FitToScreen();
        }

        // Keeps controls clear of notches and the home indicator, and scales by the shorter side
        // so the layout holds in both orientations.
        void FitToScreen()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            var safeArea = Screen.safeArea;
            if (screenSize == appliedScreenSize && safeArea == appliedSafeArea) return;
            if (float.IsNaN(root.resolvedStyle.width) || root.resolvedStyle.width <= 0) return;

            appliedScreenSize = screenSize;
            appliedSafeArea = safeArea;
            document.panelSettings.match = screenSize.x > screenSize.y ? 1f : 0f;

            var panelPerPixel = root.panel.visualTree.resolvedStyle.width / screenSize.x;
            root.style.paddingTop = (screenSize.y - safeArea.yMax) * panelPerPixel;
            root.style.paddingLeft = safeArea.xMin * panelPerPixel;
            root.style.paddingRight = (screenSize.x - safeArea.xMax) * panelPerPixel;
            sheet.style.paddingBottom = 14f + safeArea.yMin * panelPerPixel;
        }
    }
}
