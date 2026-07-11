using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Anity.Demos.URP3D
{
    public static class DemoUI
    {
        static RectTransform CreateUIElement(string name, Transform parent)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Build()
        {
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();

            var canvasGo = new GameObject("Canvas");
            var canvasRt = canvasGo.AddComponent<RectTransform>();
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasRt.anchorMin = Vector2.zero;
            canvasRt.anchorMax = Vector2.one;
            canvasRt.offsetMin = Vector2.zero;
            canvasRt.offsetMax = Vector2.zero;

            var bgRt = CreateUIElement("Background", canvasRt);
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgRt.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.3f);

            var titleRt = CreateUIElement("TitleText", canvasRt);
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0, -30);
            titleRt.sizeDelta = new Vector2(800, 60);
            var titleText = titleRt.gameObject.AddComponent<Text>();
            titleText.text = "Anity URP 3D Demo";
            titleText.fontSize = 36;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;

            var buttonPanelRt = CreateUIElement("ButtonPanel", canvasRt);
            buttonPanelRt.anchorMin = new Vector2(0f, 1f);
            buttonPanelRt.anchorMax = new Vector2(0f, 1f);
            buttonPanelRt.pivot = new Vector2(0f, 1f);
            buttonPanelRt.anchoredPosition = new Vector2(20, -100);
            buttonPanelRt.sizeDelta = new Vector2(600, 80);
            var hLayout = buttonPanelRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = new Vector2(10, 10);
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;
            var csf = buttonPanelRt.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateButton(buttonPanelRt, "ButtonRed", "Red", () => DemoScene.SetAllObjectColor(Color.red));
            CreateButton(buttonPanelRt, "ButtonGreen", "Green", () => DemoScene.SetAllObjectColor(Color.green));
            CreateButton(buttonPanelRt, "ButtonBlue", "Blue", () => DemoScene.SetAllObjectColor(Color.blue));

            var sliderPanelRt = CreateUIElement("SliderPanel", canvasRt);
            sliderPanelRt.anchorMin = new Vector2(0f, 1f);
            sliderPanelRt.anchorMax = new Vector2(0f, 1f);
            sliderPanelRt.pivot = new Vector2(0f, 1f);
            sliderPanelRt.anchoredPosition = new Vector2(20, -200);
            sliderPanelRt.sizeDelta = new Vector2(300, 40);
            var sliderBgRt = CreateUIElement("SliderBackground", sliderPanelRt);
            sliderBgRt.anchorMin = Vector2.zero;
            sliderBgRt.anchorMax = Vector2.one;
            sliderBgRt.offsetMin = Vector2.zero;
            sliderBgRt.offsetMax = Vector2.zero;
            var sliderBgImg = sliderBgRt.gameObject.AddComponent<Image>();
            sliderBgImg.color = Color.gray;
            var fillAreaRt = CreateUIElement("SliderFillArea", sliderPanelRt);
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = new Vector2(10, 0);
            fillAreaRt.offsetMax = new Vector2(-10, 0);
            var fillRt = CreateUIElement("Fill", fillAreaRt);
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(0, 1);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.color = Color.cyan;
            var handleAreaRt = CreateUIElement("SliderHandleSlideArea", sliderPanelRt);
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            var handleRt = CreateUIElement("Handle", handleAreaRt);
            handleRt.anchorMin = new Vector2(0.5f, 0.5f);
            handleRt.anchorMax = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(20, 20);
            var handleImg = handleRt.gameObject.AddComponent<Image>();
            handleImg.color = Color.white;
            var slider = sliderPanelRt.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.value = 50;
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.onValueChanged.AddListener(v => { DemoScene.TimeScaleValue = v / 50f; });

            var toggleGroupRt = CreateUIElement("ToggleGroup", canvasRt);
            toggleGroupRt.anchorMin = new Vector2(1f, 1f);
            toggleGroupRt.anchorMax = new Vector2(1f, 1f);
            toggleGroupRt.pivot = new Vector2(1f, 1f);
            toggleGroupRt.anchoredPosition = new Vector2(-20, -100);
            toggleGroupRt.sizeDelta = new Vector2(200, 120);
            var toggleGroup = toggleGroupRt.gameObject.AddComponent<ToggleGroup>();
            toggleGroup.allowSwitchOff = false;
            var vLayout = toggleGroupRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = new Vector2(5, 5);
            CreateToggle(toggleGroupRt, toggleGroup, "ToggleLow", "Low", false);
            CreateToggle(toggleGroupRt, toggleGroup, "ToggleMed", "Med", true);
            CreateToggle(toggleGroupRt, toggleGroup, "ToggleHigh", "High", false);

            var inputPanelRt = CreateUIElement("InputFieldPanel", canvasRt);
            inputPanelRt.anchorMin = new Vector2(0.5f, 1f);
            inputPanelRt.anchorMax = new Vector2(0.5f, 1f);
            inputPanelRt.pivot = new Vector2(0.5f, 1f);
            inputPanelRt.anchoredPosition = new Vector2(0, -250);
            inputPanelRt.sizeDelta = new Vector2(400, 40);
            var inputBgImg = inputPanelRt.gameObject.AddComponent<Image>();
            inputBgImg.color = new Color(1f, 1f, 1f, 0.8f);
            var placeholderRt = CreateUIElement("Placeholder", inputPanelRt);
            placeholderRt.anchorMin = Vector2.zero;
            placeholderRt.anchorMax = Vector2.one;
            placeholderRt.offsetMin = new Vector2(10, 0);
            placeholderRt.offsetMax = Vector2.zero;
            var placeholderText = placeholderRt.gameObject.AddComponent<Text>();
            placeholderText.text = "Enter text...";
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            placeholderText.fontSize = 14;
            placeholderText.alignment = TextAnchor.MiddleLeft;
            var inputTextRt = CreateUIElement("Text", inputPanelRt);
            inputTextRt.anchorMin = Vector2.zero;
            inputTextRt.anchorMax = Vector2.one;
            inputTextRt.offsetMin = new Vector2(10, 0);
            inputTextRt.offsetMax = Vector2.zero;
            var inputText = inputTextRt.gameObject.AddComponent<Text>();
            inputText.text = "";
            inputText.color = Color.black;
            inputText.fontSize = 14;
            inputText.alignment = TextAnchor.MiddleLeft;
            var inputField = inputPanelRt.gameObject.AddComponent<InputField>();
            inputField.textComponent = inputText;
            inputField.placeholder = placeholderText;
            inputField.characterLimit = 30;
            inputField.text = "AnityDemo";

            var dropdownPanelRt = CreateUIElement("DropdownPanel", canvasRt);
            dropdownPanelRt.anchorMin = new Vector2(0f, 0.5f);
            dropdownPanelRt.anchorMax = new Vector2(0f, 0.5f);
            dropdownPanelRt.pivot = new Vector2(0f, 0.5f);
            dropdownPanelRt.anchoredPosition = new Vector2(20, 0);
            dropdownPanelRt.sizeDelta = new Vector2(200, 40);
            var dropdownBgImg = dropdownPanelRt.gameObject.AddComponent<Image>();
            dropdownBgImg.color = Color.white;
            var dropdownLabelRt = CreateUIElement("Label", dropdownPanelRt);
            dropdownLabelRt.anchorMin = Vector2.zero;
            dropdownLabelRt.anchorMax = Vector2.one;
            dropdownLabelRt.offsetMin = new Vector2(10, 0);
            dropdownLabelRt.offsetMax = new Vector2(-20, 0);
            var dropdownLabelText = dropdownLabelRt.gameObject.AddComponent<Text>();
            dropdownLabelText.text = "Select Option";
            dropdownLabelText.color = Color.black;
            dropdownLabelText.fontSize = 14;
            dropdownLabelText.alignment = TextAnchor.MiddleLeft;
            var dropdownArrowRt = CreateUIElement("Arrow", dropdownPanelRt);
            dropdownArrowRt.anchorMin = new Vector2(1f, 0.5f);
            dropdownArrowRt.anchorMax = new Vector2(1f, 0.5f);
            dropdownArrowRt.pivot = new Vector2(1f, 0.5f);
            dropdownArrowRt.anchoredPosition = new Vector2(-10, 0);
            dropdownArrowRt.sizeDelta = new Vector2(20, 20);
            dropdownArrowRt.gameObject.AddComponent<Image>().color = Color.black;
            var dropdown = dropdownPanelRt.gameObject.AddComponent<Dropdown>();
            dropdown.captionText = dropdownLabelText;
            dropdown.options.AddRange(new List<Dropdown.OptionData>
            {
                new Dropdown.OptionData("Cube"),
                new Dropdown.OptionData("Sphere"),
                new Dropdown.OptionData("Capsule"),
                new Dropdown.OptionData("Particle")
            });

            var scrollViewRt = CreateUIElement("ScrollView", canvasRt);
            scrollViewRt.anchorMin = new Vector2(0.5f, 0f);
            scrollViewRt.anchorMax = new Vector2(0.5f, 0f);
            scrollViewRt.pivot = new Vector2(0.5f, 0f);
            scrollViewRt.anchoredPosition = new Vector2(0, 100);
            scrollViewRt.sizeDelta = new Vector2(400, 200);
            var viewportRt = CreateUIElement("Viewport", scrollViewRt);
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            var viewportImg = viewportRt.gameObject.AddComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0.1f);
            viewportRt.gameObject.AddComponent<RectMask2D>();
            var contentRt = CreateUIElement("Content", viewportRt);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0, 800);
            var contentLayout = contentRt.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            for (int i = 1; i <= 20; i++)
            {
                var itemRt = CreateUIElement($"Item {i}", contentRt);
                itemRt.sizeDelta = new Vector2(0, 30);
                var itemText = itemRt.gameObject.AddComponent<Text>();
                itemText.text = $"Item {i}";
                itemText.color = Color.white;
                itemText.fontSize = 14;
            }
            var scrollRect = scrollViewRt.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = contentRt;
            scrollRect.viewport = viewportRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;

            var volumePanelRt = CreateUIElement("VolumeSliderPanel", canvasRt);
            volumePanelRt.anchorMin = new Vector2(0f, 0f);
            volumePanelRt.anchorMax = new Vector2(0f, 0f);
            volumePanelRt.pivot = new Vector2(0f, 0f);
            volumePanelRt.anchoredPosition = new Vector2(20, 20);
            volumePanelRt.sizeDelta = new Vector2(200, 30);
            var volumeBgRt = CreateUIElement("VolumeBackground", volumePanelRt);
            volumeBgRt.anchorMin = Vector2.zero;
            volumeBgRt.anchorMax = Vector2.one;
            volumeBgRt.gameObject.AddComponent<Image>().color = Color.gray;
            var volumeFillRt = CreateUIElement("VolumeFill", volumeBgRt);
            volumeFillRt.anchorMin = new Vector2(0, 0);
            volumeFillRt.anchorMax = new Vector2(0, 1);
            volumeFillRt.gameObject.AddComponent<Image>().color = Color.green;
            var volumeHandleRt = CreateUIElement("VolumeHandle", volumeBgRt);
            volumeHandleRt.anchorMin = new Vector2(0.5f, 0.5f);
            volumeHandleRt.anchorMax = new Vector2(0.5f, 0.5f);
            volumeHandleRt.sizeDelta = new Vector2(15, 15);
            var volumeHandleImg = volumeHandleRt.gameObject.AddComponent<Image>();
            volumeHandleImg.color = Color.white;
            var volumeSlider = volumePanelRt.gameObject.AddComponent<Slider>();
            volumeSlider.direction = Slider.Direction.LeftToRight;
            volumeSlider.minValue = 0;
            volumeSlider.maxValue = 1;
            volumeSlider.value = 1;
            volumeSlider.fillRect = volumeFillRt;
            volumeSlider.handleRect = volumeHandleRt;
            volumeSlider.targetGraphic = volumeHandleImg;
            volumeSlider.onValueChanged.AddListener(v => { AudioListener.volume = v; });
            var volumeLabelRt = CreateUIElement("VolumeLabel", volumePanelRt);
            volumeLabelRt.anchorMin = new Vector2(1f, 0.5f);
            volumeLabelRt.anchorMax = new Vector2(1f, 0.5f);
            volumeLabelRt.pivot = new Vector2(0f, 0.5f);
            volumeLabelRt.anchoredPosition = new Vector2(10, 0);
            volumeLabelRt.sizeDelta = new Vector2(80, 30);
            var volumeLabelText = volumeLabelRt.gameObject.AddComponent<Text>();
            volumeLabelText.text = "Volume";
            volumeLabelText.color = Color.white;
            volumeLabelText.fontSize = 14;

            var fadePanelRt = CreateUIElement("FadePanel", canvasRt);
            fadePanelRt.anchorMin = Vector2.zero;
            fadePanelRt.anchorMax = Vector2.one;
            var canvasGroup = fadePanelRt.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0.2f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            var fadeTextRt = CreateUIElement("CanvasGroupText", fadePanelRt);
            fadeTextRt.anchorMin = new Vector2(0.5f, 0.5f);
            fadeTextRt.anchorMax = new Vector2(0.5f, 0.5f);
            fadeTextRt.sizeDelta = new Vector2(400, 50);
            var fadeText = fadeTextRt.gameObject.AddComponent<Text>();
            fadeText.text = "CanvasGroup Alpha Test";
            fadeText.fontSize = 24;
            fadeText.alignment = TextAnchor.MiddleCenter;
            fadeText.color = Color.white;

            var fancyTextRt = CreateUIElement("FancyText", canvasRt);
            fancyTextRt.anchorMin = new Vector2(1f, 0f);
            fancyTextRt.anchorMax = new Vector2(1f, 0f);
            fancyTextRt.pivot = new Vector2(1f, 0f);
            fancyTextRt.anchoredPosition = new Vector2(-20, 10);
            fancyTextRt.sizeDelta = new Vector2(300, 50);
            var fancyText = fancyTextRt.gameObject.AddComponent<Text>();
            fancyText.text = "Shadow + Outline";
            fancyText.fontSize = 24;
            fancyText.alignment = TextAnchor.MiddleRight;
            fancyText.color = Color.yellow;
            var shadow = fancyTextRt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(2, -2);
            var outline = fancyTextRt.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.red;
            outline.effectDistance = new Vector2(1, 1);

            var rawImageRt = CreateUIElement("RawImagePreview", canvasRt);
            rawImageRt.anchorMin = new Vector2(1f, 0.5f);
            rawImageRt.anchorMax = new Vector2(1f, 0.5f);
            rawImageRt.pivot = new Vector2(1f, 0.5f);
            rawImageRt.anchoredPosition = new Vector2(-20, 0);
            rawImageRt.sizeDelta = new Vector2(200, 150);
            var rawImage = rawImageRt.gameObject.AddComponent<RawImage>();
            rawImage.texture = DemoScene.CheckerTexture;
            rawImage.uvRect = new Rect(0, 0, 1, 1);

            var maskPanelRt = CreateUIElement("MaskPanel", canvasRt);
            maskPanelRt.anchorMin = new Vector2(1f, 0f);
            maskPanelRt.anchorMax = new Vector2(1f, 0f);
            maskPanelRt.pivot = new Vector2(1f, 1f);
            maskPanelRt.anchoredPosition = new Vector2(-120, -200);
            maskPanelRt.sizeDelta = new Vector2(100, 100);
            maskPanelRt.gameObject.AddComponent<RectMask2D>();
            var maskContentRt = CreateUIElement("MaskContent", maskPanelRt);
            maskContentRt.anchorMin = Vector2.zero;
            maskContentRt.anchorMax = Vector2.one;
            maskContentRt.anchoredPosition = new Vector2(-20, -20);
            maskContentRt.sizeDelta = new Vector2(40, 40);
            var maskContentImg = maskContentRt.gameObject.AddComponent<Image>();
            maskContentImg.color = Color.cyan;

            var gridPanelRt = CreateUIElement("GridPanel", canvasRt);
            gridPanelRt.anchorMin = new Vector2(0f, 0f);
            gridPanelRt.anchorMax = new Vector2(0f, 0f);
            gridPanelRt.pivot = new Vector2(0f, 0f);
            gridPanelRt.anchoredPosition = new Vector2(20, 100);
            gridPanelRt.sizeDelta = new Vector2(300, 150);
            var gridLayout = gridPanelRt.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(60, 60);
            gridLayout.spacing = new Vector2(10, 10);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            Color[] gridColors = { Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta, Color.white, Color.gray, Color.black };
            for (int i = 0; i < 9; i++)
            {
                var cellRt = CreateUIElement($"Cell{i}", gridPanelRt);
                var cellImg = cellRt.gameObject.AddComponent<Image>();
                cellImg.color = gridColors[i];
            }

            var progressBarRt = CreateUIElement("ProgressBar", canvasRt);
            progressBarRt.anchorMin = new Vector2(0.5f, 0f);
            progressBarRt.anchorMax = new Vector2(0.5f, 0f);
            progressBarRt.pivot = new Vector2(0.5f, 0f);
            progressBarRt.anchoredPosition = new Vector2(0, 60);
            progressBarRt.sizeDelta = new Vector2(500, 30);
            var progressBgRt = CreateUIElement("ProgressBackground", progressBarRt);
            progressBgRt.anchorMin = Vector2.zero;
            progressBgRt.anchorMax = Vector2.one;
            progressBgRt.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);
            var progressFillRt = CreateUIElement("ProgressFill", progressBgRt);
            progressFillRt.anchorMin = new Vector2(0, 0);
            progressFillRt.anchorMax = new Vector2(0, 1);
            progressFillRt.sizeDelta = Vector2.zero;
            var progressFillImg = progressFillRt.gameObject.AddComponent<Image>();
            progressFillImg.color = Color.green;
            var progressHandleRt = CreateUIElement("ProgressHandle", progressBgRt);
            progressHandleRt.anchorMin = new Vector2(0.5f, 0.5f);
            progressHandleRt.anchorMax = new Vector2(0.5f, 0.5f);
            progressHandleRt.sizeDelta = new Vector2(10, 30);
            progressHandleRt.gameObject.AddComponent<Image>().color = Color.white;
            var progressSlider = progressBarRt.gameObject.AddComponent<Slider>();
            progressSlider.direction = Slider.Direction.LeftToRight;
            progressSlider.minValue = 0;
            progressSlider.maxValue = 1;
            progressSlider.value = 0.7f;
            progressSlider.fillRect = progressFillRt;
            progressSlider.handleRect = progressHandleRt;
            var progressTextRt = CreateUIElement("ProgressText", progressBarRt);
            progressTextRt.anchorMin = Vector2.zero;
            progressTextRt.anchorMax = Vector2.one;
            var progressText = progressTextRt.gameObject.AddComponent<Text>();
            progressText.text = "70%";
            progressText.fontSize = 16;
            progressText.alignment = TextAnchor.MiddleCenter;
            progressText.color = Color.white;
        }

        private static void CreateButton(RectTransform parent, string name, string text, UnityEngine.Events.UnityAction onClick)
        {
            var buttonRt = CreateUIElement(name, parent);
            buttonRt.sizeDelta = new Vector2(100, 40);
            var buttonImg = buttonRt.gameObject.AddComponent<Image>();
            buttonImg.color = Color.white;
            var button = buttonRt.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImg;
            button.onClick.AddListener(onClick);
            var textRt = CreateUIElement("Text", buttonRt);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var btnText = textRt.gameObject.AddComponent<Text>();
            btnText.text = text;
            btnText.fontSize = 16;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.black;
        }

        private static void CreateToggle(RectTransform parent, ToggleGroup group, string name, string label, bool isOn)
        {
            var toggleRt = CreateUIElement(name, parent);
            toggleRt.sizeDelta = new Vector2(180, 30);
            var toggleBgRt = CreateUIElement("Background", toggleRt);
            toggleBgRt.anchorMin = new Vector2(0f, 0.5f);
            toggleBgRt.anchorMax = new Vector2(0f, 0.5f);
            toggleBgRt.pivot = new Vector2(0f, 0.5f);
            toggleBgRt.sizeDelta = new Vector2(20, 20);
            toggleBgRt.gameObject.AddComponent<Image>().color = Color.white;
            var checkmarkRt = CreateUIElement("Checkmark", toggleBgRt);
            checkmarkRt.anchorMin = Vector2.zero;
            checkmarkRt.anchorMax = Vector2.one;
            checkmarkRt.offsetMin = new Vector2(4, 4);
            checkmarkRt.offsetMax = new Vector2(-4, -4);
            checkmarkRt.gameObject.AddComponent<Image>().color = Color.black;
            var labelRt = CreateUIElement("Label", toggleRt);
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(30, 0);
            labelRt.offsetMax = Vector2.zero;
            var labelText = labelRt.gameObject.AddComponent<Text>();
            labelText.text = label;
            labelText.fontSize = 14;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = Color.white;
            var toggle = toggleRt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = toggleBgRt.gameObject.GetComponent<Image>();
            toggle.graphic = checkmarkRt.gameObject.GetComponent<Image>();
            toggle.group = group;
            toggle.isOn = isOn;
        }
    }
}
