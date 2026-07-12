namespace UnityEditor;

using UnityEngine;

public static class EditorStyles
{
  private static Font? _boldFont;
  public static Font boldFont { get { _boldFont ??= new Font(); return _boldFont; } }

  public static GUIStyle label { get; } = new() { name = "label" };
  public static GUIStyle boldLabel { get; } = new() { name = "boldLabel", fontSize = 12, fontStyle = FontStyle.Bold };
  public static GUIStyle centeredBoldLabel { get; } = new() { name = "centeredBoldLabel", fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
  public static GUIStyle miniLabel { get; } = new() { name = "miniLabel", fontSize = 10 };
  public static GUIStyle miniBoldLabel { get; } = new() { name = "miniBoldLabel", fontSize = 10, fontStyle = FontStyle.Bold };
  public static GUIStyle whiteLabel { get; } = new() { name = "whiteLabel" };
  public static GUIStyle whiteBoldLabel { get; } = new() { name = "whiteBoldLabel", fontStyle = FontStyle.Bold };
  public static GUIStyle whiteMiniLabel { get; } = new() { name = "whiteMiniLabel", fontSize = 10 };
  public static GUIStyle redLabel { get; } = new() { name = "redLabel" };
  public static GUIStyle yellowLabel { get; } = new() { name = "yellowLabel" };
  public static GUIStyle greyLabel { get; } = new() { name = "greyLabel" };
  public static GUIStyle centeredGreyMiniLabel { get; } = new() { name = "centeredGreyMiniLabel", fontSize = 10, alignment = TextAnchor.MiddleCenter };
  public static GUIStyle highlightLabel { get; } = new() { name = "highlightLabel" };
  public static GUIStyle selectedLabel { get; } = new() { name = "selectedLabel" };
  public static GUIStyle wordWrappedLabel { get; } = new() { name = "wordWrappedLabel", wordWrap = true };
  public static GUIStyle wordWrappedMiniLabel { get; } = new() { name = "wordWrappedMiniLabel", fontSize = 10, wordWrap = true };
  public static GUIStyle rightAlignedLabel { get; } = new() { name = "rightAlignedLabel", alignment = TextAnchor.MiddleRight };
  public static GUIStyle largeLabel { get; } = new() { name = "largeLabel", fontSize = 16 };
  public static GUIStyle largeBoldLabel { get; } = new() { name = "largeBoldLabel", fontSize = 16, fontStyle = FontStyle.Bold };

  public static GUIStyle toolbar { get; } = new() { name = "toolbar" };
  public static GUIStyle toolbarButton { get; } = new() { name = "toolbarButton" };
  public static GUIStyle toolbarDropDown { get; } = new() { name = "toolbarDropDown" };
  public static GUIStyle toolbarPopup { get; } = new() { name = "toolbarPopup" };
  public static GUIStyle toolbarSearchField { get; } = new() { name = "toolbarSearchField" };
  public static GUIStyle toolbarSearchFieldCancelButton { get; } = new() { name = "toolbarSearchFieldCancelButton" };
  public static GUIStyle toolbarSearchFieldCancelButtonEmpty { get; } = new() { name = "toolbarSearchFieldCancelButtonEmpty" };
  public static GUIStyle toolbarTextField { get; } = new() { name = "toolbarTextField" };

  public static GUIStyle foldout { get; } = new() { name = "foldout", fontSize = 11 };
  public static GUIStyle boldFoldout { get; } = new() { name = "boldFoldout", fontSize = 11 };
  public static GUIStyle inspectorDefaultMargins { get; } = new() { name = "inspectorDefaultMargins" };
  public static GUIStyle inspectorTitlebar { get; } = new() { name = "inspectorTitlebar" };
  public static GUIStyle inspectorTitlebarText { get; } = new() { name = "inspectorTitlebarText" };

  public static GUIStyle textField { get; } = new() { name = "textField" };
  public static GUIStyle boldTextField { get; } = new() { name = "boldTextField", fontStyle = FontStyle.Bold };
  public static GUIStyle textArea { get; } = new() { name = "textArea" };
  public static GUIStyle numberField { get; } = new() { name = "numberField" };
  public static GUIStyle toggle { get; } = new() { name = "toggle" };
  public static GUIStyle radiobutton { get; } = new() { name = "radiobutton" };
  public static GUIStyle popup { get; } = new() { name = "popup" };
  public static GUIStyle enumPopup { get; } = new() { name = "enumPopup" };
  public static GUIStyle layerField { get; } = new() { name = "layerField" };
  public static GUIStyle tagField { get; } = new() { name = "tagField" };
  public static GUIStyle maskField { get; } = new() { name = "maskField" };

  public static GUIStyle objectField { get; } = new() { name = "objectField" };
  public static GUIStyle objectFieldThumb { get; } = new() { name = "objectFieldThumb" };
  public static GUIStyle colorField { get; } = new() { name = "colorField" };
  public static GUIStyle curveField { get; } = new() { name = "curveField" };
  public static GUIStyle gradientField { get; } = new() { name = "gradientField" };
  public static GUIStyle vector2Field { get; } = new() { name = "vector2Field" };
  public static GUIStyle vector3Field { get; } = new() { name = "vector3Field" };
  public static GUIStyle vector4Field { get; } = new() { name = "vector4Field" };

  public static GUIStyle button { get; } = new() { name = "button" };
  public static GUIStyle minibutton { get; } = new() { name = "minibutton", fontSize = 10 };
  public static GUIStyle miniButton => minibutton;
  public static GUIStyle miniButtonLeft { get; } = new() { name = "miniButtonLeft" };
  public static GUIStyle miniButtonMid { get; } = new() { name = "miniButtonMid" };
  public static GUIStyle miniButtonRight { get; } = new() { name = "miniButtonRight" };

  public static GUIStyle box { get; } = new() { name = "box" };
  public static GUIStyle helpBox { get; } = new() { name = "helpBox" };
  public static GUIStyle scrollView { get; } = new() { name = "scrollView" };
  public static GUIStyle horizontalScrollbar { get; } = new() { name = "horizontalScrollbar" };
  public static GUIStyle verticalScrollbar { get; } = new() { name = "verticalScrollbar" };

  public static GUIStyle slider { get; } = new() { name = "slider" };
  public static GUIStyle sliderThumb { get; } = new() { name = "sliderThumb" };
  public static GUIStyle horizontalSlider { get; } = new() { name = "horizontalSlider" };
  public static GUIStyle horizontalSliderThumb { get; } = new() { name = "horizontalSliderThumb" };
  public static GUIStyle verticalSlider { get; } = new() { name = "verticalSlider" };
  public static GUIStyle verticalSliderThumb { get; } = new() { name = "verticalSliderThumb" };
  public static GUIStyle horizontalScrollbarThumb { get; } = new() { name = "horizontalScrollbarThumb" };
  public static GUIStyle verticalScrollbarThumb { get; } = new() { name = "verticalScrollbarThumb" };

  public static GUIStyle preButton { get; } = new() { name = "preButton" };
  public static GUIStyle preLabel { get; } = new() { name = "preLabel" };
  public static GUIStyle preTextField { get; } = new() { name = "preTextField" };
  public static GUIStyle preSlider { get; } = new() { name = "preSlider" };
  public static GUIStyle preSliderThumb { get; } = new() { name = "preSliderThumb" };
  public static GUIStyle prePopup { get; } = new() { name = "prePopup" };

  public static GUIStyle statusbar { get; } = new() { name = "statusbar" };
  public static GUIStyle dragHandle { get; } = new() { name = "dragHandle" };
  public static GUIStyle notification { get; } = new() { name = "notification" };
  public static GUIStyle notificationText { get; } = new() { name = "notificationText" };

  public static GUIStyle gameViewBackground { get; } = new() { name = "gameViewBackground" };
  public static GUIStyle projectBrowserIconDropShadow { get; } = new() { name = "projectBrowserIconDropShadow" };
  public static GUIStyle projectBrowserHeaderBgTop { get; } = new() { name = "projectBrowserHeaderBgTop" };
  public static GUIStyle projectBrowserHeaderBgBottom { get; } = new() { name = "projectBrowserHeaderBgBottom" };
  public static GUIStyle projectBrowserIconAreaBgTop { get; } = new() { name = "projectBrowserIconAreaBgTop" };
  public static GUIStyle projectBrowserIconAreaBgBottom { get; } = new() { name = "projectBrowserIconAreaBgBottom" };
  public static GUIStyle projectBrowserSidebar { get; } = new() { name = "projectBrowserSidebar" };
  public static GUIStyle projectBrowserSelectionGrid { get; } = new() { name = "projectBrowserSelectionGrid" };
  public static GUIStyle projectBrowserAlignedLabel { get; } = new() { name = "projectBrowserAlignedLabel" };
  public static GUIStyle projectBrowserLabel { get; } = new() { name = "projectBrowserLabel" };
  public static GUIStyle projectBrowserSubAssetLabel { get; } = new() { name = "projectBrowserSubAssetLabel" };
  public static GUIStyle projectBrowserGridLabel { get; } = new() { name = "projectBrowserGridLabel" };

  public static GUIStyle treeView { get; } = new() { name = "treeView" };
  public static GUIStyle treeViewBackground { get; } = new() { name = "treeViewBackground" };
  public static GUIStyle treeViewItem { get; } = new() { name = "treeViewItem" };
  public static GUIStyle treeViewItemSelected { get; } = new() { name = "treeViewItemSelected" };
  public static GUIStyle treeViewItemActive { get; } = new() { name = "treeViewItemActive" };
  public static GUIStyle treeViewItemInactive { get; } = new() { name = "treeViewItemInactive" };
  public static GUIStyle treeViewRenamingField { get; } = new() { name = "treeViewRenamingField" };

  public static GUIStyle sectionHeader { get; } = new() { name = "sectionHeader" };
  public static GUIStyle sectionHeaderLabel { get; } = new() { name = "sectionHeaderLabel" };
  public static GUIStyle boldLabelOnDark { get; } = new() { name = "boldLabelOnDark" };
  public static GUIStyle titleLabel { get; } = new() { name = "titleLabel", fontSize = 18 };
  public static GUIStyle header { get; } = new() { name = "header", fontStyle = FontStyle.Bold, fontSize = 14 };
}
