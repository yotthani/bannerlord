using System.Collections.Generic;
using System.Text;

namespace BannerlordThemeSwitcher
{
    /// <summary>
    /// Generates brush XML definitions dynamically from a ColorScheme.
    /// This allows themes to define colors centrally and have all brushes update automatically.
    /// </summary>
    public static class BrushTemplates
    {
        /// <summary>
        /// Generate complete brush XML for a theme based on its color scheme
        /// </summary>
        public static string GenerateBrushXml(Theme theme)
        {
            var colors = theme.Colors;
            var sb = new StringBuilder();
            
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine($"<!-- Auto-generated brushes for {theme.Name} theme -->");
            sb.AppendLine("<Brushes>");
            sb.AppendLine();
            
            // Generate all brush categories
            GenerateCharacterCreationBrushes(sb, theme.Id, colors);
            GeneratePopupBrushes(sb, theme.Id, colors);
            GenerateStandardUIBrushes(sb, theme.Id, colors);
            GenerateEscapeMenuBrushes(sb, theme.Id, colors);
            GenerateEncyclopediaBrushes(sb, theme.Id, colors);
            GenerateConversationBrushes(sb, theme.Id, colors);
            GenerateFaceGenBrushes(sb, theme.Id, colors);
            GenerateNotificationBrushes(sb, theme.Id, colors);
            GenerateInventoryBrushes(sb, theme.Id, colors);
            GeneratePartyScreenBrushes(sb, theme.Id, colors);
            GenerateMapBrushes(sb, theme.Id, colors);
            
            sb.AppendLine("</Brushes>");
            
            return sb.ToString();
        }

        #region Character Creation Brushes
        
        private static void GenerateCharacterCreationBrushes(StringBuilder sb, string themeId, ColorScheme c)
        {
            string suffix = $".{themeId}";
            
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine($"  <!-- CHARACTER CREATION - {themeId} -->");
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine();
            
            // Culture button frame
            sb.AppendLine($"  <Brush Name=\"CharacterCreation.Culture.Button.Frame{suffix}\" TransitionDuration=\"0.22\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"rounded_frame_9\" Color=\"{Hex(c.Border)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Default\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.Border)}\" /></Style>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BorderHighlight)}\" /></Style>");
            sb.AppendLine($"      <Style Name=\"Selected\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BorderHighlight)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Culture button background
            sb.AppendLine($"  <Brush Name=\"CharacterCreation.Culture.Button{suffix}\" TransitionDuration=\"0.22\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"rounded_canvas_9\" Color=\"{Hex(c.ButtonBackground)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.ButtonHover)}\" /></Style>");
            sb.AppendLine($"      <Style Name=\"Selected\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.ButtonPressed)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Stage title text
            sb.AppendLine($"  <Brush Name=\"Stage.Title.Text{suffix}\" Font=\"Galahad\" TextHorizontalAlignment=\"Center\">");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Default\" FontColor=\"{Hex(c.TextTitle)}\" TextOutlineColor=\"{Hex(c.Shadow)}\" TextOutlineAmount=\"0.1\" FontSize=\"42\" />");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Culture text
            sb.AppendLine($"  <Brush Name=\"Culture.Text{suffix}\">");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" />");
            sb.AppendLine($"      <Style Name=\"Hovered\" FontColor=\"{Hex(c.TextHighlight)}\" />");
            sb.AppendLine($"      <Style Name=\"Selected\" FontColor=\"{Hex(c.TextHighlight)}\" />");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Generic button text
            sb.AppendLine($"  <Brush Name=\"Generic.Button.Text{suffix}\" Font=\"FiraSansExtraCondensed-Regular\">");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" FontSize=\"32\" />");
            sb.AppendLine($"      <Style Name=\"Hovered\" FontColor=\"{Hex(c.TextHighlight)}\" FontSize=\"32\" />");
            sb.AppendLine($"      <Style Name=\"Pressed\" FontColor=\"{Hex(c.Primary)}\" FontSize=\"32\" />");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Stage descriptions
            sb.AppendLine($"  <Brush Name=\"Stage.Description.Text{suffix}\" Font=\"FiraSansExtraCondensed-Regular\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" FontSize=\"32\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine($"  <Brush Name=\"Stage.Selection.Description.Text{suffix}\" Font=\"FiraSansExtraCondensed-Regular\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.TextMuted)}\" FontSize=\"23\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Selection frame
            sb.AppendLine($"  <Brush Name=\"CharacterCreation.Selection.Frame{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"frame_9\" Color=\"{Hex(c.Border)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine();
        }
        
        #endregion
        
        #region Popup/Dialog Brushes
        
        private static void GeneratePopupBrushes(StringBuilder sb, string themeId, ColorScheme c)
        {
            string suffix = $".{themeId}";
            
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine($"  <!-- POPUPS & DIALOGS - {themeId} -->");
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine();
            
            // Popup frame
            sb.AppendLine($"  <Brush Name=\"Popup.Frame{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"frame_9\" Color=\"{Hex(c.Border)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Popup background
            sb.AppendLine($"  <Brush Name=\"Popup.Background{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"BlankWhiteSquare_9\" Color=\"{Hex(c.BackgroundDark)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Popup title
            sb.AppendLine($"  <Brush Name=\"Popup.Title.Text{suffix}\" Font=\"Galahad\" TextHorizontalAlignment=\"Center\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.TextTitle)}\" FontSize=\"36\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Popup description
            sb.AppendLine($"  <Brush Name=\"Popup.Description.Text{suffix}\" Font=\"FiraSansExtraCondensed-Regular\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" FontSize=\"20\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Popup button text
            sb.AppendLine($"  <Brush Name=\"Popup.Button.Text{suffix}\" Font=\"Galahad\" TextHorizontalAlignment=\"Center\">");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" FontSize=\"24\" />");
            sb.AppendLine($"      <Style Name=\"Hovered\" FontColor=\"{Hex(c.TextHighlight)}\" FontSize=\"24\" />");
            sb.AppendLine($"      <Style Name=\"Pressed\" FontColor=\"{Hex(c.Primary)}\" FontSize=\"24\" />");
            sb.AppendLine($"      <Style Name=\"Disabled\" FontColor=\"{Hex(c.TextDisabled)}\" FontSize=\"24\" />");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Popup button frame
            sb.AppendLine($"  <Brush Name=\"Popup.Button.Frame{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"button_frame_9\" Color=\"{Hex(c.ButtonBorder)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BorderHighlight)}\" /></Style>");
            sb.AppendLine($"      <Style Name=\"Disabled\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BorderMuted)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Popup button background
            sb.AppendLine($"  <Brush Name=\"Popup.Button.Background{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"button_canvas_9\" Color=\"{Hex(c.ButtonBackground)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.ButtonHover)}\" /></Style>");
            sb.AppendLine($"      <Style Name=\"Pressed\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.ButtonPressed)}\" /></Style>");
            sb.AppendLine($"      <Style Name=\"Disabled\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.ButtonDisabled)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Info text
            sb.AppendLine($"  <Brush Name=\"Popup.Info.Text{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Info)}\" FontSize=\"18\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Warning text
            sb.AppendLine($"  <Brush Name=\"Popup.Warning.Text{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Warning)}\" FontSize=\"18\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine();
        }
        
        #endregion
        
        #region Standard UI Brushes
        
        private static void GenerateStandardUIBrushes(StringBuilder sb, string themeId, ColorScheme c)
        {
            string suffix = $".{themeId}";
            
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine($"  <!-- STANDARD UI ELEMENTS - {themeId} -->");
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine();
            
            // Back button
            sb.AppendLine($"  <Brush Name=\"BackButton{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"button_canvas_9\" Color=\"{Hex(c.ButtonBackground)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.ButtonHover)}\" /></Style>");
            sb.AppendLine($"      <Style Name=\"Pressed\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.ButtonPressed)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Back button text
            sb.AppendLine($"  <Brush Name=\"BackButton.Text{suffix}\">");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" />");
            sb.AppendLine($"      <Style Name=\"Hovered\" FontColor=\"{Hex(c.TextHighlight)}\" />");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Dropdown item
            sb.AppendLine($"  <Brush Name=\"DropdownItem{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Background)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundHover)}\" /></Style>");
            sb.AppendLine($"      <Style Name=\"Selected\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundSelected)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Header text
            sb.AppendLine($"  <Brush Name=\"Header.Text{suffix}\" Font=\"Galahad\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.TextTitle)}\" FontSize=\"32\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Subheader text
            sb.AppendLine($"  <Brush Name=\"SubHeader.Text{suffix}\" Font=\"FiraSansExtraCondensed-Medium\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Primary)}\" FontSize=\"24\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Body text
            sb.AppendLine($"  <Brush Name=\"Body.Text{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" FontSize=\"18\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Muted text
            sb.AppendLine($"  <Brush Name=\"Muted.Text{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.TextMuted)}\" FontSize=\"16\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Panel frame
            sb.AppendLine($"  <Brush Name=\"Panel.Frame{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"frame_9\" Color=\"{Hex(c.Border)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Panel background
            sb.AppendLine($"  <Brush Name=\"Panel.Background{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"BlankWhiteSquare_9\" Color=\"{Hex(c.Background)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Scrollbar
            sb.AppendLine($"  <Brush Name=\"Scrollbar{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundLight)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine($"  <Brush Name=\"Scrollbar.Thumb{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Border)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BorderHighlight)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Divider
            sb.AppendLine($"  <Brush Name=\"Divider{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.BorderMuted)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Tab
            sb.AppendLine($"  <Brush Name=\"Tab{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Background)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundHover)}\" /></Style>");
            sb.AppendLine($"      <Style Name=\"Selected\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundSelected)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine($"  <Brush Name=\"Tab.Text{suffix}\">");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Default\" FontColor=\"{Hex(c.TextMuted)}\" />");
            sb.AppendLine($"      <Style Name=\"Hovered\" FontColor=\"{Hex(c.Text)}\" />");
            sb.AppendLine($"      <Style Name=\"Selected\" FontColor=\"{Hex(c.TextHighlight)}\" />");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Checkbox
            sb.AppendLine($"  <Brush Name=\"Checkbox{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"rounded_frame_9\" Color=\"{Hex(c.Border)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Selected\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.Primary)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Slider
            sb.AppendLine($"  <Brush Name=\"Slider.Track{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundLight)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine($"  <Brush Name=\"Slider.Fill{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Primary)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine($"  <Brush Name=\"Slider.Thumb{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"BlankWhiteCircle\" Color=\"{Hex(c.Primary)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.TextHighlight)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Progress bar
            sb.AppendLine($"  <Brush Name=\"ProgressBar.Background{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundDark)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine($"  <Brush Name=\"ProgressBar.Fill{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Primary)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine();
        }
        
        #endregion
        
        #region Escape Menu Brushes
        
        private static void GenerateEscapeMenuBrushes(StringBuilder sb, string themeId, ColorScheme c)
        {
            string suffix = $".{themeId}";
            
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine($"  <!-- ESCAPE MENU - {themeId} -->");
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine();
            
            // Menu background
            sb.AppendLine($"  <Brush Name=\"EscapeMenu.Background{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"esc_menu_frame_canvas_9\" Color=\"{Hex(c.BackgroundDark)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Menu frame
            sb.AppendLine($"  <Brush Name=\"EscapeMenu.Frame{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"frame_9\" Color=\"{Hex(c.Border)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Menu button
            sb.AppendLine($"  <Brush Name=\"EscapeMenu.Button{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.ButtonBackground)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.ButtonHover)}\" /></Style>");
            sb.AppendLine($"      <Style Name=\"Pressed\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.ButtonPressed)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Menu button text
            sb.AppendLine($"  <Brush Name=\"EscapeMenu.Button.Text{suffix}\" Font=\"Galahad\">");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" FontSize=\"28\" />");
            sb.AppendLine($"      <Style Name=\"Hovered\" FontColor=\"{Hex(c.TextHighlight)}\" FontSize=\"28\" />");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine();
        }
        
        #endregion
        
        #region Encyclopedia Brushes
        
        private static void GenerateEncyclopediaBrushes(StringBuilder sb, string themeId, ColorScheme c)
        {
            string suffix = $".{themeId}";
            
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine($"  <!-- ENCYCLOPEDIA - {themeId} -->");
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine();
            
            // Encyclopedia frame
            sb.AppendLine($"  <Brush Name=\"Encyclopedia.Frame{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"frame_9\" Color=\"{Hex(c.Border)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Encyclopedia title
            sb.AppendLine($"  <Brush Name=\"Encyclopedia.Title{suffix}\" Font=\"Galahad\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.TextTitle)}\" FontSize=\"36\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Encyclopedia link
            sb.AppendLine($"  <Brush Name=\"Encyclopedia.Link{suffix}\">");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Default\" FontColor=\"{Hex(c.Primary)}\" />");
            sb.AppendLine($"      <Style Name=\"Hovered\" FontColor=\"{Hex(c.TextHighlight)}\" />");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Encyclopedia entry
            sb.AppendLine($"  <Brush Name=\"Encyclopedia.Entry{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Background)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundHover)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Encyclopedia divider
            sb.AppendLine($"  <Brush Name=\"Encyclopedia.Divider{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"list_divider_9\" Color=\"{Hex(c.BorderMuted)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine();
        }
        
        #endregion
        
        #region Conversation Brushes
        
        private static void GenerateConversationBrushes(StringBuilder sb, string themeId, ColorScheme c)
        {
            string suffix = $".{themeId}";
            
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine($"  <!-- CONVERSATIONS - {themeId} -->");
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine();
            
            // Dialog frame
            sb.AppendLine($"  <Brush Name=\"Conversation.Frame{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"conversation_frame_9\" Color=\"{Hex(c.Border)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Dialog background
            sb.AppendLine($"  <Brush Name=\"Conversation.Background{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"conversation_frame_canvas_9\" Color=\"{Hex(c.BackgroundDark)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // NPC name
            sb.AppendLine($"  <Brush Name=\"Conversation.NPCName{suffix}\" Font=\"Galahad\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.TextTitle)}\" FontSize=\"28\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Dialog text
            sb.AppendLine($"  <Brush Name=\"Conversation.Text{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" FontSize=\"20\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Dialog option
            sb.AppendLine($"  <Brush Name=\"Conversation.Option{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"dialog_option_canvas_9\" Color=\"{Hex(c.Background)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundHover)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Dialog option text
            sb.AppendLine($"  <Brush Name=\"Conversation.Option.Text{suffix}\">");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" FontSize=\"18\" />");
            sb.AppendLine($"      <Style Name=\"Hovered\" FontColor=\"{Hex(c.TextHighlight)}\" FontSize=\"18\" />");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Persuasion bar
            sb.AppendLine($"  <Brush Name=\"Conversation.Persuasion.Success{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Success)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine($"  <Brush Name=\"Conversation.Persuasion.Fail{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Error)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine();
        }
        
        #endregion
        
        #region FaceGen Brushes
        
        private static void GenerateFaceGenBrushes(StringBuilder sb, string themeId, ColorScheme c)
        {
            string suffix = $".{themeId}";
            
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine($"  <!-- FACE GENERATION - {themeId} -->");
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine();
            
            // FaceGen panel
            sb.AppendLine($"  <Brush Name=\"FaceGen.Panel{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundDark)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // FaceGen frame
            sb.AppendLine($"  <Brush Name=\"FaceGen.Frame{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"frame_9\" Color=\"{Hex(c.Border)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // FaceGen category
            sb.AppendLine($"  <Brush Name=\"FaceGen.Category{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Background)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundHover)}\" /></Style>");
            sb.AppendLine($"      <Style Name=\"Selected\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundSelected)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // FaceGen slider
            sb.AppendLine($"  <Brush Name=\"FaceGen.Slider{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Primary)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine();
        }
        
        #endregion
        
        #region Notification Brushes
        
        private static void GenerateNotificationBrushes(StringBuilder sb, string themeId, ColorScheme c)
        {
            string suffix = $".{themeId}";
            
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine($"  <!-- NOTIFICATIONS & CHAT - {themeId} -->");
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine();
            
            // Notification background
            sb.AppendLine($"  <Brush Name=\"Notification.Background{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundDark)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Notification frame
            sb.AppendLine($"  <Brush Name=\"Notification.Frame{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"frame_9\" Color=\"{Hex(c.Border)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Notification text
            sb.AppendLine($"  <Brush Name=\"Notification.Text{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" FontSize=\"16\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Success notification
            sb.AppendLine($"  <Brush Name=\"Notification.Success{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Success)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Warning notification
            sb.AppendLine($"  <Brush Name=\"Notification.Warning{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Warning)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Error notification
            sb.AppendLine($"  <Brush Name=\"Notification.Error{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Error)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Chat message
            sb.AppendLine($"  <Brush Name=\"Chat.Message{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" FontSize=\"14\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Chat highlight
            sb.AppendLine($"  <Brush Name=\"Chat.Highlight{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.TextHighlight)}\" FontSize=\"14\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine();
        }
        
        #endregion
        
        #region Inventory Brushes
        
        private static void GenerateInventoryBrushes(StringBuilder sb, string themeId, ColorScheme c)
        {
            string suffix = $".{themeId}";
            
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine($"  <!-- INVENTORY - {themeId} -->");
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine();
            
            // Item slot
            sb.AppendLine($"  <Brush Name=\"Inventory.Slot{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"rounded_canvas_9\" Color=\"{Hex(c.Background)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundHover)}\" /></Style>");
            sb.AppendLine($"      <Style Name=\"Selected\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundSelected)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Item slot frame
            sb.AppendLine($"  <Brush Name=\"Inventory.Slot.Frame{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"rounded_frame_9\" Color=\"{Hex(c.BorderMuted)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.Border)}\" /></Style>");
            sb.AppendLine($"      <Style Name=\"Selected\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BorderHighlight)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Item name
            sb.AppendLine($"  <Brush Name=\"Inventory.ItemName{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" FontSize=\"18\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Gold text
            sb.AppendLine($"  <Brush Name=\"Inventory.Gold{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Gold)}\" FontSize=\"18\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Weight text
            sb.AppendLine($"  <Brush Name=\"Inventory.Weight{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.TextMuted)}\" FontSize=\"14\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine();
        }
        
        #endregion
        
        #region Party Screen Brushes
        
        private static void GeneratePartyScreenBrushes(StringBuilder sb, string themeId, ColorScheme c)
        {
            string suffix = $".{themeId}";
            
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine($"  <!-- PARTY SCREEN - {themeId} -->");
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine();
            
            // Troop card
            sb.AppendLine($"  <Brush Name=\"Party.TroopCard{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Background)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Hovered\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundHover)}\" /></Style>");
            sb.AppendLine($"      <Style Name=\"Selected\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundSelected)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Troop card frame
            sb.AppendLine($"  <Brush Name=\"Party.TroopCard.Frame{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"frame_9\" Color=\"{Hex(c.BorderMuted)}\" /></Layers>");
            sb.AppendLine($"    <Styles>");
            sb.AppendLine($"      <Style Name=\"Selected\"><BrushLayer Name=\"Default\" Color=\"{Hex(c.Border)}\" /></Style>");
            sb.AppendLine($"    </Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Troop name
            sb.AppendLine($"  <Brush Name=\"Party.TroopName{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" FontSize=\"16\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Troop count
            sb.AppendLine($"  <Brush Name=\"Party.TroopCount{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.TextMuted)}\" FontSize=\"14\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Morale bar
            sb.AppendLine($"  <Brush Name=\"Party.Morale{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.Morale)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Food indicator
            sb.AppendLine($"  <Brush Name=\"Party.Food.Positive{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Success)}\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine($"  <Brush Name=\"Party.Food.Negative{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Error)}\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine();
        }
        
        #endregion
        
        #region Map Brushes
        
        private static void GenerateMapBrushes(StringBuilder sb, string themeId, ColorScheme c)
        {
            string suffix = $".{themeId}";
            
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine($"  <!-- MAP UI - {themeId} -->");
            sb.AppendLine($"  <!-- ══════════════════════════════════════════════════════════════════════════ -->");
            sb.AppendLine();
            
            // Map bar background
            sb.AppendLine($"  <Brush Name=\"Map.Bar.Background{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundDark)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Map bar frame
            sb.AppendLine($"  <Brush Name=\"Map.Bar.Frame{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"frame_9\" Color=\"{Hex(c.Border)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Map tooltip
            sb.AppendLine($"  <Brush Name=\"Map.Tooltip{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Color=\"{Hex(c.BackgroundDark)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Map tooltip frame
            sb.AppendLine($"  <Brush Name=\"Map.Tooltip.Frame{suffix}\">");
            sb.AppendLine($"    <Layers><BrushLayer Name=\"Default\" Sprite=\"frame_9\" Color=\"{Hex(c.Border)}\" /></Layers>");
            sb.AppendLine($"  </Brush>");
            
            // Settlement name
            sb.AppendLine($"  <Brush Name=\"Map.Settlement.Name{suffix}\" Font=\"Galahad\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.TextTitle)}\" FontSize=\"20\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Time display
            sb.AppendLine($"  <Brush Name=\"Map.Time{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Text)}\" FontSize=\"16\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Gold display
            sb.AppendLine($"  <Brush Name=\"Map.Gold{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Gold)}\" FontSize=\"18\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            // Influence display
            sb.AppendLine($"  <Brush Name=\"Map.Influence{suffix}\">");
            sb.AppendLine($"    <Styles><Style Name=\"Default\" FontColor=\"{Hex(c.Experience)}\" FontSize=\"18\" /></Styles>");
            sb.AppendLine($"  </Brush>");
            
            sb.AppendLine();
        }
        
        #endregion
        
        #region Helper Methods
        
        private static string Hex(TaleWorlds.Library.Color color)
        {
            return ColorScheme.ColorToHex(color);
        }
        
        #endregion
    }
}
