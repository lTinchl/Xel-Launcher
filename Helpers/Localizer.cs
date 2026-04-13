namespace XelLauncher.Helpers
{
    public class Localizer : AntdUI.ILocalization
    {
        public string GetLocalizedString(string key)
        {
            switch (key)
            {
                case "ID":
                    return "en-US";

                case "Cancel":
                    return "Cancel";
                case "OK":
                    return "OK";
                case "Now":
                    return "Now";
                case "ToDay":
                    return "Today";
                case "NoData":
                    return "No data";

                case "Mon":
                    return "Mon";
                case "Tue":
                    return "Tue";
                case "Wed":
                    return "Wed";
                case "Thu":
                    return "Thu";
                case "Fri":
                    return "Fri";
                case "Sat":
                    return "Sat";
                case "Sun":
                    return "Sun";

                case "ItemsPerPage":
                    return "Per/Page";

                case "Filter":
                    return "Filter";
                case "Filter.Clean":
                    return "Clean";
                case "Filter.SelectAll":
                    return "(Select All)";
                case "Filter.Blank":
                    return "(Blank)";
                case "Filter.Search":
                    return "Search";

                case "Filter.Equal":
                    return "Equal";
                case "Filter.NotEqual":
                    return "NotEqual";
                case "Filter.Greater":
                    return "Greater";
                case "Filter.Less":
                    return "Less";
                case "Filter.Contain":
                    return "Contain";
                case "Filter.NotContain":
                    return "NotContain";
                case "Filter.None":
                    return "None";
                case "Table.Summary.SUM":
                    return "SUM";
                case "Table.Summary.AVG":
                    return "AVG";
                case "Table.Summary.MIN":
                    return "MIN";
                case "Table.Summary.MAX":
                    return "MAX";
                case "Table.Summary.COUNT":
                    return "COUNT";
                case "Table.Summary.NONE":
                    return "None";

                #region DEMO

                case "Overview.txt_search"://输入关键字搜索...
                    return "Keywords to search";
                case "CopyOK":
                    return "copied";
                case "CopyFailed":
                    return "copy failed";

                case "General":
                    return "General";
                case "Layout":
                    return "Layout";
                case "Navigation":
                    return "Navigation";
                case "DataEntry":
                    return "Data Entry";
                case "DataDisplay":
                    return "Data Display";
                case "Feedback":
                    return "Feedback";
                case "Other":
                    return "Other";

                case "Setting":
                    return "Setting";
                case "AnimationEnabled":
                    return "Animation Enabled";
                case "ShadowEnabled":
                    return "Shadow Enabled";
                case "PopupWindow":
                    return "Popup in the window";
                case "ScrollBarHidden":
                    return "ScrollBar Hidden Style";
                case "TextRenderingHighQuality":
                    return "TextRendering HighQuality";

                case "Previous":
                    return "Previous";
                case "Next":
                    return "Next";
                case "Finish":
                    return "Finish";
                case "Click:":
                    return "Click:";

                //Alert ----------------------------
                case "Alert":
                    return "Alert";
                case "Alert.Description":
                    return "Display warning messages that require attention.";
                case "Alert.divider1":
                    return "More types";
                case "Alert.divider2":
                    return "Description";
                case "Alert.divider3":
                    return "Loop Banner";
                case "Alert.alert14":
                    return "Nike Just Do It";

                //Avatar ----------------------------
                case "Avatar":
                    return "Avatar";
                case "Avatar.Description":
                    return "Used to represent users or things, supporting the display of images, icons, or characters.";
                case "Avatar.avatar5":
                    return "N";

                //Badge ----------------------------
                case "Badge":
                    return "Badge";
                case "Badge.Description":
                    return "Small numerical value or status descriptor for UI elements.";
                case "Badge.divider1":
                    return "Basic";
                case "Badge.divider2":
                    return "More";
                case "Badge.tag1":
                    return "GAMES";

                //Breadcrumb ----------------------------
                case "Breadcrumb":
                    return "Breadcrumb";
                case "Breadcrumb.Description":
                    return "Display the current location within a hierarchy. And allow going back to states higher up in the hierarchy.";

                //Button ----------------------------
                case "Button":
                    return "Button";
                case "Button.Description":
                    return "To trigger an operation.";
                case "Button.Search":
                    return "Search";
                case "Button.divider1":
                    return "Color & Type";
                case "Button.divider2":
                    return "Icon";
                case "Button.divider3":
                    return "Link";
                case "Button.divider4":
                    return "IconPosition";
                case "Button.divider5":
                    return "Multiple Buttons";
                case "Button.divider6":
                    return "Shape";

                //FloatButton ----------------------------
                case "FloatButton":
                    return "FloatButton";
                case "FloatButton.1":
                    return "Search for";
                case "FloatButton.2":
                    return "Don't be ridiculous";
                case "FloatButton.3":
                    return "Help me";
                case "FloatButton.4":
                    return "Hopeless";
                case "FloatButton.Arrow":
                    return "Expand";

                //Carousel ----------------------------
                case "Carousel":
                    return "Carousel";
                case "Carousel.Description":
                    return "A set of carousel areas.";

                //Checkbox ----------------------------
                case "Checkbox":
                    return "Checkbox";
                case "Checkbox.Description":
                    return "Collect user's choices.";
                case "Checkbox.divider1":
                    return "Basic";
                case "Checkbox.divider2":
                    return "Custom color";
                case "Checkbox.divider3":
                    return "Linkage";

                //Collapse ----------------------------
                case "Collapse":
                    return "Collapse";
                case "Collapse.Description":
                    return "A content area which can be collapsed and expanded.";
                case "Collapse.divider1":
                    return "Collapse";
                case "Collapse.divider2":
                    return "Borderless";

                //ColorPicker ----------------------------
                case "ColorPicker":
                    return "ColorPicker";
                case "ColorPicker.Description":
                    return "Used for color selection.";

                //DatePicker ----------------------------
                case "DatePicker":
                    return "DatePicker";
                case "DatePicker.Description":
                    return "To select or input a date.";
                case "DatePicker.divider1":
                    return "Basic";
                case "DatePicker.divider2":
                    return "Range Picker";
                case "DatePicker.divider3":
                    return "Time/Preset";
                case "DatePicker.PlaceholderText":
                    return "Select a date";
                case "DatePicker.PlaceholderS":
                    return "Start date";
                case "DatePicker.PlaceholderE":
                    return "End date";

                //Divider ----------------------------
                case "Divider":
                    return "Divider";
                case "Divider.Description":
                    return "A divider line separates different content.";

                //GridPanel ----------------------------
                case "GridPanel":
                    return "GridPanel";
                case "GridPanel.Description":
                    return "Grid layout container with precise division of areas.";
                case "GridPanel.Prefix":
                    return "Span Attribute";
                case "GridPanel.Describe":
                    return "-The column width attribute is before, and the row height attribute is after; grouping. Each row represents the number of row height attributes corresponding to the number of rows, with spaces separating each number";

                //Splitter ----------------------------
                case "Splitter":
                    return "Splitter";
                case "Splitter.Description":
                    return "Split panels to isolate";

                //Drawer ----------------------------
                case "Drawer":
                    return "Drawer";
                case "Drawer.Description":
                    return "A panel that slides out from the edge of the screen.";
                case "Drawer.divider1":
                    return "Basic";

                //Dropdown ----------------------------
                case "Dropdown":
                    return "Dropdown";
                case "Dropdown.Description":
                    return "A dropdown list.";
                case "Dropdown.divider1":
                    return "Type";
                case "Dropdown.divider2":
                    return "Placement";
                case "Dropdown.dropdown1":
                    return "Subs menu";

                //Icon ----------------------------
                case "Icon":
                    return "Icon";
                case "Icon.Description":
                    return "Semantic vector graphics.";
                case "Icon.PlaceholderText":
                    return "Search icons here, click icon to copy code";
                case "Outlined": return "Outlined";
                case "Filled": return "Filled";
                case "Icon.Directional": return "Directional Icons";
                case "Icon.Suggested": return "Suggested Icons";
                case "Icon.Editor": return "Editor Icons";
                case "Icon.Data": return "Data Icons";
                case "Icon.Logos": return "Brand and Logos";
                case "Icon.Application": return "Application Icons";

                //Input ----------------------------
                case "Input":
                    return "Input";
                case "Input.Description":
                    return "Through mouse or keyboard input content, it is the most basic form field wrapper.";
                case "Input.Search":
                    return "Search";
                case "Input.divider1":
                    return "Basic";
                case "Input.divider2":
                    return "Pre / Post tab";
                case "Input.divider3":
                    return "Variant";
                case "Input.divider4":
                    return "Multiline";
                case "Input.divider5":
                    return "Password";
                case "Input.divider6":
                    return "Combination";
                case "Input.Code":
                    return "Please enter verification code";
                case "Input.Code2":
                    return "Code: ";
                case "Input.Tao":
                    return "Tao, I like it";
                case "Input.input2":
                case "Input.input14":
                    return "Clear button";
                case "Input.input3":
                    return "Round";
                case "Input.input4":
                    return "Bold border";
                case "Input.input5":
                    return "Please input something";
                case "Input.input13":
                    return "Enter your password";
                case "Input.input10":
                case "Input.input18":
                case "Input.input19":
                    return "input search text";

                //InputNumber ----------------------------
                case "InputNumber":
                    return "InputNumber";
                case "InputNumber.Description":
                    return "Enter a number within certain range with the mouse or keyboard.";
                case "InputNumber.divider1":
                    return "Basic";
                case "InputNumber.input3":
                    return "Enter number";

                //Menu ----------------------------
                case "Menu":
                    return "Menu";
                case "Menu.Description":
                    return "A versatile menu for navigation.";
                case "Menu.divider1":
                    return "Top Navigation";
                case "Menu.divider2":
                    return "Inline menu";
                case "Menu.divider3":
                    return "Vertical menu";
                case "Menu.expand":
                    return "Expand";
                case "Menu.collapse":
                    return "Collapse";
                case "Menu.flatten":
                    return "Flatten";
                case "Menu.indent":
                    return "Indent";

                //Message ----------------------------
                case "Message":
                    return "Message";
                case "Message.Description":
                    return "Display global messages as feedback in response to user operations.";
                case "Message.divider1":
                    return "More types";
                case "Message.divider2":
                    return "Message with loading indicator";
                case "Message.divider3":
                    return "System Sound";

                //Modal ----------------------------
                case "Modal":
                    return "Modal";
                case "Modal.Description":
                    return "Display a modal dialog box, providing a title, content area, and action buttons.";
                case "Modal.divider1":
                    return "Basic";

                //Notification ----------------------------
                case "Notification":
                    return "Notification";
                case "Notification.Description":
                    return "Prompt notification message globally.";
                case "Notification.divider1":
                    return "Placement";
                case "Notification.divider2":
                    return "More types";
                case "Notification.divider3":
                    return "System Sound";

                //PageHeader ----------------------------
                case "PageHeader":
                    return "PageHeader";
                case "PageHeader.Description":
                    return "A header with common actions and design elements built in.";
                case "PageHeader.Type":
                    return "ShowBack";
                case "PageHeader.divider1":
                    return "Basic";
                case "PageHeader.divider2":
                    return "CloseButton";

                //Pagination ----------------------------
                case "Pagination":
                    return "Pagination";
                case "Pagination.Description":
                    return "A long list can be divided into several pages, and only one page will be loaded at a time.";

                //Panel ----------------------------
                case "Panel":
                    return "Panel";
                case "Panel.Description":
                    return "A container for displaying information.";

                //Popover ----------------------------
                case "Popover":
                    return "Popover";
                case "Popover.Description":
                    return "The floating card pops up when clicking/mouse hovering over an element.";
                case "Popover.divider1":
                    return "Basic";
                case "Popover.divider2":
                    return "Placement";
                case "Popover.button1":
                    return "Normal";
                case "Popover.button2":
                    return "Custom Control Content";

                //Preview ----------------------------
                case "Preview":
                    return "Preview";
                case "Preview.Description":
                    return "Picture preview box.";
                case "Preview.divider1":
                    return "Basic";
                case "Preview.button1":
                    return "Single Image";
                case "Preview.button2":
                    return "Multiple Images";
                case "Preview.button3":
                    return "Dynamic Load Images";
                case "Preview.button4":
                    return "Multiple images and Text previews";

                //Progress ----------------------------
                case "Progress":
                    return "Progress";
                case "Progress.Description":
                    return "Display the current progress of the operation.";
                case "Progress.divider1":
                    return "Standard progress bar";
                case "Progress.divider2":
                    return "Circular progress bar";
                case "Progress.divider3":
                    return "Mini size progress bar";
                case "Progress.divider4":
                    return "Responsive circular progress bar";
                case "Progress.divider5":
                    return "Progress bar with steps";

                //Radio ----------------------------
                case "Radio":
                    return "Radio";
                case "Radio.Description":
                    return "Used to select a single state from multiple options.";
                case "Radio.divider1":
                    return "Basic";
                case "Radio.divider2":
                    return "Custom color";
                case "Radio.divider3":
                    return "Linkage";

                //Rate ----------------------------
                case "Rate":
                    return "Rate";
                case "Rate.Description":
                    return "Used for rating operation on something.";
                case "Rate.rate5":
                    return "A";
                case "Rate.rate6":
                    return "NB";

                //Result ----------------------------
                case "Result":
                    return "Result";
                case "Result.Description":
                    return "Used to feedback the processing results of a series of operations.";

                //Segmented ----------------------------
                case "Segmented":
                    return "Segmented";
                case "Segmented.Description":
                    return "Display multiple options and allow users to select a single option.";

                //Select ----------------------------
                case "Select":
                    return "Select";
                case "Select.Description":
                    return "A dropdown menu for displaying choices.";
                case "Select.divider1":
                    return "Basic";
                case "Select.divider2":
                    return "Combination";
                case "Select.divider3":
                    return "More";
                case "Select.select4":
                    return "No text";
                case "Select.select5":
                    return "Show Arrow";
                case "Select.select6":
                case "Select.select7":
                    return "Enter search freely";
                case "Select.select8":
                    return "(Select)";
                case "Select.sub menu 1":
                    return "Sub menu 1";
                case "Select.sub menu 2":
                    return "Sub menu 2";

                //Slider ----------------------------
                case "Slider":
                    return "Slider";
                case "Slider.Description":
                    return "A Slider component for displaying current value and intervals in range.";
                case "Slider.divider1":
                    return "Basic";
                case "Slider.divider2":
                    return "Mark Dot";

                //Steps ----------------------------
                case "Steps":
                    return "Steps";
                case "Steps.Description":
                    return "A navigation bar that guides users through the steps of a task.";
                case "Steps.CurrentCompleted":
                    return "Current Completed";
                case "Steps.CurrentProcessing":
                    return "Current Processing";

                //Switch ----------------------------
                case "Switch":
                    return "Switch";
                case "Switch.Description":
                    return "Used to toggle between two states.";

                //Table ----------------------------
                case "Table":
                    return "Table";
                case "Table.Description":
                    return "A table displays rows of data.";
                case "Table.checkFixedHeader":
                    return "FixedHeader";
                case "Table.checkColumnDragSort":
                    return "ColumnDragSort";
                case "Table.checkRowsDragSort":
                    return "RowsDragSort";
                case "Table.checkBordered":
                    return "Bordered";
                case "Table.checkSetRowStyle":
                    return "SetRowStyle";
                case "Table.checkSortOrder":
                    return "SortOrder";
                case "Table.checkEnableHeaderResizing":
                    return "EnableHeaderResizing";
                case "Table.checkVisibleHeader":
                    return "VisibleHeader";
                case "Table.checkAddressLineBreak":
                    return "AddressLineBreak";
                case "Table.checkFilter":
                    return "Filter";
                case "Table.selectEditMode":
                    return "EditMode";
                case "Table.selectEditStyle":
                    return "EditStyle";
                case "Table.selectFocusedStyle":
                    return "FocusedStyle";
                case "Table.checkTree":
                    return "Tree";
                case "Table.checkScrollBarAvoidHeader":
                    return "ScrollBar AvoidHeader";
                case "Table.checkboxFocusNavigation":
                    return "FocusNavigation";
                case "Table.checkboxSummaryCustomize":
                    return "CustomSummary";
                case "Table.Column.name":
                    return "Name";
                case "Table.Column.checkTitle":
                    return "No Title";
                case "Table.Column.radio":
                    return "Radio";
                case "Table.Column.online":
                    return "Online";
                case "Table.Column.enable":
                    return "Enable";
                case "Table.Column.age":
                    return "Age";
                case "Table.Column.hobby":
                    return "Hobby";
                case "Table.Column.address":
                    return "Address";
                case "Table.Column.date":
                    return "Date";
                case "Table.Column.imgs":
                    return "Imgs";
                case "Table.Column.btns":
                    return "Action";
                case "Table.Data.Name1":
                    return "John Brown";
                case "Table.Data.Name2":
                    return "Jim Green";
                case "Table.Data.Name3":
                    return "Joe Black";
                case "Table.Data.Online.Default":
                    return "Default";
                case "Table.Data.Online":
                    return "Online";
                case "Table.Data.Online.Processing":
                    return "Processing";
                case "Table.Data.Online.Error":
                    return "Error";
                case "Table.Data.Online.Warn":
                    return "Warn";
                case "Table.Data.Address1":
                    return "London, Park Lane no.1";
                case "Table.Data.Address2":
                    return "New York No.1 Lake Park";
                case "Table.Data.Address3":
                    return "London No. 1 Lake Park";
                case "Table.Data.Address4":
                    return "Sydney No. 1 Lake Park";
                case "Table.Data.AddressNum":
                    return "London, Park Lane no.";
                case "Table.Data.Books":
                    return "Books";
                case "Table.Data.Travel":
                    return "Travel";
                case "Table.Data.Social":
                    return "Social";
                case "Table.Data.Sports":
                    return "Sports";
                case "Table.Data.FormatSummary":
                    return "{0} {1} in total";

                //Tabs ----------------------------
                case "Tabs":
                    return "Tabs";
                case "Tabs.Description":
                    return "Tabs make it easy to explore and switch between different views.";
                case "Tabs.divider1":
                    return "Basic";
                case "Tabs.divider2":
                    return "Card style";
                case "Tabs.divider3":
                    return "Center position";

                //Tag ----------------------------
                case "Tag":
                    return "Tag";
                case "Tag.Description":
                    return "Used for marking and categorization.";
                case "Tag.divider1":
                    return "Basic";
                case "Tag.divider2":
                    return "Colorful Tag";
                case "Tag.divider3":
                    return "Icon";
                case "Tag.tag16":
                    return "Custom Icon";

                //Timeline ----------------------------
                case "Timeline":
                    return "Timeline";
                case "Timeline.Description":
                    return "Vertical display timeline.";

                //TimePicker ----------------------------
                case "TimePicker":
                    return "TimePicker";
                case "TimePicker.Description":
                    return "To select/input a time.";
                case "TimePicker.divider1":
                    return "Basic";

                //Tooltip ----------------------------
                case "Tooltip":
                    return "Tooltip";
                case "Tooltip.Description":
                    return "Simple text popup box.";
                case "Tooltip.divider1":
                    return "Basic";
                case "Tooltip.divider2":
                    return "Placement";
                case "Tooltip.label4":
                    return "Simplest usage";

                //Tour ----------------------------
                case "Tour":
                    return "Tour";
                case "Tour.Description":
                    return "A popup component for guiding users through a product.";

                //Tree ----------------------------
                case "Tree":
                    return "Tree";
                case "Tree.Description":
                    return "Multiple-level structure list.";
                case "Tree.Loading":
                    return "Load animation, click pause";

                //VirtualPanel ----------------------------
                case "VirtualPanel":
                    return "VirtualPanel";
                case "VirtualPanel.Description":
                    return "Layout container detached from Winform framework.";
                case "VirtualPanel.checkbox1":
                    return "Waterfall";

                //Calendar ----------------------------
                case "Calendar":
                    return "Calendar";
                case "Calendar.Description":
                    return "A container that displays data in calendar form.";
                case "Calendar.divider1":
                    return "Basic";

                //ContextMenuStrip ----------------------------
                case "ContextMenuStrip":
                    return "ContextMenuStrip";
                case "ContextMenuStrip.Description":
                    return "Right click on the current page at will";

                //Battery ----------------------------
                case "Battery":
                    return "Battery";
                case "Battery.Description":
                    return "Display device battery level.";
                case "Battery.Add":
                    return "Power up";
                case "Battery.Subtract":
                    return "Reduce";
                case "Battery.divider1":
                    return "Basic";
                case "Battery.divider2":
                    return "No text";
                case "Battery.divider3":
                    return "Point size";

                //Signal ----------------------------
                case "Signal":
                    return "Signal";
                case "Signal.Description":
                    return "Display device signals.";
                case "Signal.Add":
                    return "Add";
                case "Signal.Subtract":
                    return "Subtract";
                case "Signal.divider1":
                    return "Basic";
                case "Signal.divider2":
                    return "Line style";
                case "Signal.divider3":
                    return "Loading";

                //Spin ----------------------------
                case "Spin":
                    return "Spin";
                case "Spin.Description":
                    return "Used for the loading status of a page or a block.";
                case "Spin.divider1":
                    return "Direct use";
                case "Spin.divider2":
                    return "Display Text";
                case "Spin.divider3":
                    return "Basic";
                case "Spin.btnPanel":
                    return "Current container";
                case "Spin.btnControl":
                    return "Control above";
                case "Spin.btnWindow":
                    return "Entire window";
                case "Spin.buttonError":
                    return "Error callback";

                //Shield ----------------------------
                case "Shield":
                    return "Shield";
                case "Shield.Description":
                    return "Concise, consistent, and legible badges.";
                case "Shield.qq":
                    return "QQ Group";

                //Watermark ----------------------------
                case "Watermark":
                    return "Watermark";
                case "Watermark.Description":
                    return "Add specific text or patterns to the page.";
                case "Watermark.lblContent":
                    return "Watermark content:";
                case "Watermark.lblSub":
                    return "Sub content:";
                case "Watermark.lblForeColor":
                    return "Watermark color:";
                case "Watermark.lblOpacity":
                    return "Opacity:";
                case "Watermark.lblRotate":
                    return "Rotation angle:";
                case "Watermark.lblGap":
                    return "Gap:";

                case "Watermark.btnForm":
                    return "Window watermark";
                case "Watermark.btnFormError":
                    return "Error occurred while creating form watermark:";
                case "Watermark.btnFormFailed":
                    return "Form watermark creation failed";
                case "Watermark.btnFormOK":
                    return "Form watermark created successfully!";

                case "Watermark.btnPanel":
                    return "Panel watermark";
                case "Watermark.btnPanelError":
                    return "Error occurred while creating panel watermark:";
                case "Watermark.btnPanelFailed":
                    return "Panel watermark creation failed";
                case "Watermark.btnPanelOK":
                    return "Panel watermark created successfully!";

                case "Watermark.btnClear":
                    return "Clear watermark";

                case "Transfer":
                    return "Transfer";
                case "Transfer.Description":
                    return "Double column transfer choice box.";
                case "Transfer.One":
                    return "One Way";
                case "Transfer.Reload":
                    return "Reload";
                case "Transfer.Items":
                    return " items";
                case "Transfer.Content":
                    return "content";
                case "Transfer.Option":
                    return "option";
                case "Transfer.Source":
                    return "Source";
                case "Transfer.Target":
                    return "Target";
                case "Transfer.SourceT":
                    return "Source: ";
                case "Transfer.TargetT":
                    return "Target: ";
                case "Transfer.search":
                    return "Search here";

                //Chart ----------------------------
                case "Chart":
                    return "Chart";
                case "Chart.Description":
                    return "Visual chart library.";

                //HyperlinkLabel ----------------------------
                case "HyperlinkLabel":
                    return "HyperlinkLabel";
                case "HyperlinkLabel.Description":
                    return "Hyperlink text<a>";
                case "HyperlinkLabel.divider1":
                    return "Basic";
                case "HyperlinkLabel.divider2":
                    return "Center alignment";
                case "HyperlinkLabel.divider3":
                    return "Link with badge";
                case "HyperlinkLabel.divider4":
                    return "Custom Style";
                case "HyperlinkLabel.divider5":
                    return "Multiple links";

                #endregion

                case "Loading":
                    return "LOADING";
                case "Processing":
                    return "Processing";
                case "Loading2":
                    return "Loading in progress...";
                case "PleaseWait":
                    return "Please be patient and wait";

                // ── XelLauncher App strings ──────────────────────────────

                // Overview toolbar
                case "App.Lang.Chinese":
                    return "中文";
                case "App.Lang.English":
                    return "English";
                case "App.Menu.Help":
                    return "Help";
                case "App.Menu.About":
                    return "About";
                case "App.Sidebar.Delete":
                    return "Delete";
                case "App.BgColor.White":
                    return "Pure White";
                case "App.BgColor.Mint":
                    return "Mint Green";
                case "App.BgColor.Warm":
                    return "Warm Beige";
                case "App.BgColor.Sky":
                    return "Sky Blue";
                case "App.BgColor.Custom":
                    return "Custom...";
                case "App.BgColor.DialogTitle":
                    return "Custom Background Color";
                case "App.BgColor.OK":
                    return "OK";
                case "App.BgColor.Cancel":
                    return "Cancel";

                // Tray
                case "App.Tray.Show":
                    return "Show Window";
                case "App.Tray.Exit":
                    return "Exit";

                // GamePage
                case "App.Game.SelectAccount":
                    return "  Select Account";
                case "App.Game.Start":
                    return "Start Game";
                case "App.Game.Setting":
                    return "Game Settings";
                case "App.Game.AccountManage":
                    return "Account Management";
                case "App.Game.Toolbox":
                    return "Toolbox";
                case "App.Game.SelectDirTitle":
                    return "Select [{0}] Game Root Directory";
                case "App.Game.WarnSelectDir":
                    return "Please select the game root directory first";
                case "App.Game.ExeNotFound":
                    return "Could not find {0} in the selected directory";
                case "App.Game.Loading":
                    return "Loading...";
                case "App.Game.SwitchingAccount":
                    return "Switching account...";
                case "App.Game.LaunchSuccess":
                    return "Game launched successfully";
                case "App.Game.HardLinkTip":
                    return "Tip: Install the launcher on the same drive as your game to enable hard links for instant server switching";

                // Switch progress
                case "App.Switch.ConfirmTitle":
                    return "Switch Server";
                case "App.Switch.ConfirmMsg":
                    return "Switching servers requires closing the running game. Continue?";
                case "App.Switch.ConfirmOk":
                    return "Close & Switch";
                case "App.Switch.KillingProcess":
                    return "Stopping game process...";
                case "App.Switch.Linking":
                    return "Switching server (hard link)...";
                case "App.Switch.Copying":
                    return "Copying files...";
                case "App.Switch.Extracting":
                    return "Extracting files...";
                case "App.Switch.DoneHardLink":
                    return "Server switched (hard link)";
                case "App.Switch.DoneCopy":
                    return "Server switched (file copy)";
                case "App.Switch.NoPayload":
                    return "Server resources not found (no folder or ZIP in load directory)";

                // GameSettingForm
                case "App.GameSetting.VersionEndfield":
                    return "Version: v1.1.9";
                case "App.GameSetting.VersionArknights":
                    return "Version: v72.0.0";
                case "App.GameSetting.InstallPath":
                    return "Game Install Path";
                case "App.GameSetting.PathPlaceholder":
                    return "Path not set";
                case "App.GameSetting.ChangePath":
                    return "Change Path";
                case "App.GameSetting.OpenDir":
                    return "Open File Directory";
                case "App.GameSetting.ReplaceOfficial":
                    return "Replace files with Official server";
                case "App.GameSetting.ConfirmReplaceArkOfficial":
                    return "Replace the current directory with Official server files? This will overwrite game files.";
                case "App.GameSetting.ZipNotFoundOfficial":
                    return "Official server resource package (ArkOfficial.zip) not found. Check the load folder.";
                case "App.GameSetting.ReplaceBili":
                    return "Replace files with Bilibili server";
                case "App.GameSetting.ReplaceGlobal":
                    return "Replace files with Global server";
                case "App.GameSetting.BiliWebsite":
                    return "Arknights BiliBili Website";
                case "App.GameSetting.EndfieldWebsite":
                    return "Endfield Official Website";
                case "App.GameSetting.EndfieldBiliWebsite":
                    return "Endfield BiliBili Website";
                case "App.GameSetting.EndfieldGlobalWebsite":
                    return "Endfield Global Website";
                case "App.GameSetting.ArknightsWebsite":
                    return "Arknights Official Website";
                case "App.GameSetting.SyncToBili":
                    return "Sync path to Bilibili server";
                case "App.GameSetting.SyncToAll":
                    return "Sync path to Bilibili / Global server";
                case "App.GameSetting.WarnSetBiliPath":
                    return "Please set the Bilibili server path first";
                case "App.GameSetting.WarnSetGlobalPath":
                    return "Please set the Global server path first";
                case "App.GameSetting.WarnSetOfficialPath":
                    return "Please set the Official server path first";
                case "App.GameSetting.ZipNotFoundBili":
                    return "Bilibili server resource package (ArkBilibili.zip) not found. Check the load folder.";
                case "App.GameSetting.ZipNotFoundEndBili":
                    return "Bilibili server resource package (EndBilibili.zip) not found. Check the load folder.";
                case "App.GameSetting.ZipNotFoundGlobal":
                    return "Global server resource package (EndGlobal.zip) not found. Check the load folder.";
                case "App.GameSetting.ConfirmReplace":
                    return "Confirm Replace";
                case "App.GameSetting.ConfirmReplaceArkBili":
                    return "Replace the current Official server files with Bilibili server files? This will overwrite game files.";
                case "App.GameSetting.ConfirmReplaceEndBili":
                    return "Replace the current directory with Bilibili server files? This will overwrite game files.";
                case "App.GameSetting.ConfirmReplaceGlobal":
                    return "Replace the current directory with Global server files? This will overwrite game files.";
                case "App.GameSetting.Replacing":
                    return "Replacing...";
                case "App.GameSetting.ReplaceSuccess":
                    return "Success! Server resource package has been applied.";
                case "App.GameSetting.ReplaceFailed":
                    return "Replace failed: ";
                case "App.GameSetting.SyncSuccess":
                    return "Path synced to Bilibili server.";
                case "App.GameSetting.SyncSuccessAll":
                    return "Path synced to Bilibili and Global servers.";
                case "App.GameSetting.PathInvalid":
                    return "Invalid Path";
                case "App.GameSetting.PathInvalidMsg":
                    return "{0} not found in the selected folder. Please choose the correct game root directory.";
                case "App.GameSetting.Reselect":
                    return "Reselect";
                case "App.GameSetting.CustomSync":
                    return "Custom Launch Sync";
                case "App.GameSetting.ManageSync":
                    return "Manage Sync Apps";

                // Setting panel
                case "App.Setting.Software":
                    return "Software";
                case "App.Setting.Log":
                    return "Log";
                case "App.Setting.MinimizeToTray":
                    return "Minimize to Tray";
                case "App.Setting.StartWithWindows":
                    return "Boot Startup";
                case "App.Setting.CloseAfterLaunch":
                    return "Close after launching game";
                case "App.Setting.HideToTrayOnLaunch":
                    return "Hide to tray after launching game";
                case "App.Setting.UseExternalBrowser":
                    return "Use external browser for links";

                // BrowserForm
                case "App.Browser.Title":
                    return "Browser";
                case "App.Browser.NoRuntime":
                    return "WebView2 Runtime not found, please install it first!";

                // AccountManagerForm
                case "App.Account.ColName":
                    return "Account Name";
                case "App.Account.ColDefault":
                    return "Default";
                case "App.Account.ColStatus":
                    return "Status";
                case "App.Account.ColEnabled":
                    return "Enabled";
                case "App.Account.ColAction":
                    return "Actions";
                case "App.Account.TagDefault":
                    return "Default";
                case "App.Account.BadgeDisabled":
                    return "Disabled";
                case "App.Account.BadgeEnabled":
                    return "Enabled";
                case "App.Account.BtnRecord":
                    return "Save";
                case "App.Account.BtnSetDefault":
                    return "Set Default";
                case "App.Account.BtnRename":
                    return "Rename";
                case "App.Account.BtnDelete":
                    return "Delete";
                case "App.Account.BtnConfirmDelete":
                    return "Confirm Delete";
                case "App.Account.BtnDone":
                    return "Done";
                case "App.Account.BtnAdd":
                    return "+ Add Account";
                case "App.Account.SaveLoading":
                    return "Saving...";
                case "App.Account.SaveOK":
                    return "Account \"{0}\" saved";
                case "App.Account.AddTitle":
                    return "Add Account";
                case "App.Account.AddPlaceholder":
                    return "Enter account name";
                case "App.Account.RenameTitle":
                    return "Rename Account";
                case "App.Account.RenamePlaceholder":
                    return "Enter new name";

                // GameIconPickerForm
                case "App.Picker.Title":
                    return "Select a game to add";
                case "App.Picker.AlreadyAdded":
                    return "\"{0}\" is already in the list.";

                // SyncAppManagerForm
                case "App.Sync.Title":
                    return "Sync App Manager";
                case "App.Sync.BtnAdd":
                    return "+ Add";
                case "App.Sync.BtnBack":
                    return "← Back";
                case "App.Sync.Empty":
                    return "No sync apps yet";
                case "App.Sync.EmptySub":
                    return "Click \"+ Add\" in the top right to add an app";
                case "App.Sync.BtnDelete":
                    return "Delete";
                case "App.Sync.DialogTitle":
                    return "Select a program to launch";
                case "App.Sync.DialogFilter":
                    return "Executable files (*)|*";
                case "App.Sync.AlreadyAdded":
                    return "This app is already in the list";
                case "App.Sync.AddSuccess":
                    return "Added: {0}";

                case "smileys emotion":
                    return "Smileys & Emotion";
                case "people body":
                    return "People & Body";
                case "animals nature":
                    return "Animals & Nature";
                case "food drink":
                    return "Food & Drink";
                case "travel places":
                    return "Travel & Places";
                case "activities":
                    return "Activities";
                case "objects":
                    return "Objects";
                case "symbols":
                    return "Symbols";
                case "flags":
                    return "Flags";

                default:
                    System.Diagnostics.Debug.WriteLine(key);
                    return null;
            }
        }
    }
}