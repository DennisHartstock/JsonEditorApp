using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes; // Required for JsonNode
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml.Input;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace JsonEditorApp
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private JsonNode _loadedJsonNode;
        private StorageFile _currentFile;

        // Manually define expected types for specific arrays (simplified)
        private Dictionary<string, List<string>> _expectedArrayItemTypes = new Dictionary<string, List<string>>()
        {
            { "AxesConfiguration.AxisNodes[].CollisionActors", new List<string>() { "CollisionBox" } }, // CollisionActors contains CollisionBox
             { "AxesConfiguration.StageControllers", new List<string>() { "StageTest", "StageBR" } },
             { "AxesConfiguration.Axes", new List<string>() { "AxisBR", "AxisStandard" } },
             { "Detectors", new List<string>() { "DetectorSL" } }, // Add other detector types as needed
             { "Sources", new List<string>() { "TestSource", "SourceHamamatsu" } }, // Add other source types as needed
             { "Startup", new List<string>() { "StartupDevice" } } // Assuming a generic StartupDevice type for items
        };

        public MainWindow()
        {
            this.InitializeComponent();
        }

        private async void OpenJsonFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".json");

            StorageFile file = await openPicker.PickSingleFileAsync();

            if (file != null)
            {
                try
                {
                    string jsonContent = await FileIO.ReadTextAsync(file);

                    _loadedJsonNode = JsonNode.Parse(jsonContent);
                    _currentFile = file;

                    JsonTreeItemsControl.Items.Clear();

                    DisplayJsonNode(_loadedJsonNode, JsonTreeItemsControl, "");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing JSON: {ex.Message}");
                    _loadedJsonNode = null;
                    JsonTreeItemsControl.Items.Clear();
                    _currentFile = null;
                    ShowError($"Error loading JSON file: {ex.Message}");
                }
            }
        }

        private void DisplayJsonNode(JsonNode node, ItemsControl parentItemsControl, string currentPath)
        {
            if (node == null) return;

            switch (node.GetValueKind())
            {
                case JsonValueKind.Object:
                    if (node.AsObject().TryGetPropertyValue("$type", out var typeNode) && typeNode.GetValueKind() == JsonValueKind.String)
                    {
                        string type = typeNode.GetValue<string>();
                        DisplayTypedObject(node.AsObject(), parentItemsControl, currentPath, type);
                    }
                    else
                    {
                        var objectExpander = new Expander();
                        var objectHeaderStackPanel = new StackPanel() { Orientation = Orientation.Horizontal, Spacing = 10 };
                        objectHeaderStackPanel.Children.Add(new TextBlock() { Text = GetPathSegment(currentPath) ?? "Object", FontWeight = Microsoft.UI.Text.FontWeights.Bold });

                        var addPropertyButton = new Button() { Content = "+ Property", Tag = currentPath, FontSize = 10, Padding = new Thickness(5, 0, 5, 0) };
                        addPropertyButton.Click += AddPropertyButton_Click;
                        objectHeaderStackPanel.Children.Add(addPropertyButton);

                        objectExpander.Header = objectHeaderStackPanel;

                        var objectItemsControl = new ItemsControl() { Margin = new Thickness(10, 0, 0, 0) };
                        objectExpander.Content = objectItemsControl;

                        if (parentItemsControl != null)
                        {
                            parentItemsControl.Items.Add(objectExpander);
                        }

                        foreach (var property in node.AsObject())
                        {
                            string newPath = string.IsNullOrEmpty(currentPath) ? property.Key : $"{currentPath}.{property.Key}";
                            DisplayJsonNode(property.Value, objectItemsControl, newPath);
                        }
                    }
                    break;
                case JsonValueKind.Array:
                    var arrayExpander = new Expander();
                    var arrayHeaderStackPanel = new StackPanel() { Orientation = Orientation.Horizontal, Spacing = 10 };
                    arrayHeaderStackPanel.Children.Add(new TextBlock() { Text = GetPathSegment(currentPath) ?? "Array", FontWeight = Microsoft.UI.Text.FontWeights.Bold });

                    var addItemButton = new Button() { Content = "+ Item", Tag = currentPath, FontSize = 10, Padding = new Thickness(5, 0, 5, 0) };
                    addItemButton.Click += AddItemButton_Click;
                    arrayHeaderStackPanel.Children.Add(addItemButton);

                    arrayExpander.Header = arrayHeaderStackPanel;

                    var arrayItemsControl = new ItemsControl() { Margin = new Thickness(10, 0, 0, 0) };
                    arrayExpander.Content = arrayItemsControl;

                    if (parentItemsControl != null)
                    {
                        parentItemsControl.Items.Add(arrayExpander);
                    }

                    int index = 0;
                    foreach (var item in node.AsArray())
                    {
                        string newPath = string.IsNullOrEmpty(currentPath) ? $"[{index}]" : $"{currentPath}[{index}]";
                        DisplayJsonNode(item, arrayItemsControl, newPath);
                        index++;
                    }
                    break;
                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    // Handle simple values with DisplayJsonProperty
                    DisplayJsonProperty(node.Parent as JsonObject, parentItemsControl as StackPanel, GetParentPath(currentPath), GetPathSegment(currentPath), isEditable: true, node.AsValue()); // Pass JsonValue
                    break;
            }
        }

        // Modified to use specific display methods for typed objects
        private void DisplayTypedObject(JsonObject jsonObject, ItemsControl parentItemsControl, string currentPath, string type)
        {
            var typeExpander = new Expander();
            var typeHeaderStackPanel = new StackPanel() { Orientation = Orientation.Horizontal, Spacing = 10 };
            typeHeaderStackPanel.Children.Add(new TextBlock() { Text = $"{GetPathSegment(currentPath) ?? "Item"} ({type})", FontWeight = Microsoft.UI.Text.FontWeights.Bold });

            var deleteButton = new Button() { Content = "x", Tag = currentPath, FontSize = 10, Padding = new Thickness(5, 0, 5, 0) };
            deleteButton.Click += DeleteButton_Click;
            typeHeaderStackPanel.Children.Add(deleteButton);

            typeExpander.Header = typeHeaderStackPanel;

            var typeContentPanel = new StackPanel() { Margin = new Thickness(10, 0, 0, 0) };
            typeExpander.Content = typeContentPanel;

            if (parentItemsControl != null)
            {
                parentItemsControl.Items.Add(typeExpander);
            }

            // Call specific display method based on type
            switch (type)
            {
                case "CollisionBox":
                    DisplayCollisionBox(jsonObject, typeContentPanel, currentPath);
                    break;
                case "StageTest":
                    DisplayStageTest(jsonObject, typeContentPanel, currentPath);
                    break;
                case "StageBR":
                    DisplayStageBR(jsonObject, typeContentPanel, currentPath);
                    break;
                case "AxisBR":
                    DisplayAxisBR(jsonObject, typeContentPanel, currentPath);
                    break;
                case "AxisStandard":
                    DisplayAxisStandard(jsonObject, typeContentPanel, currentPath);
                    break;
                case "DetectorSL":
                    DisplayDetectorSL(jsonObject, typeContentPanel, currentPath);
                    break;
                case "TestSource":
                    DisplayTestSource(jsonObject, typeContentPanel, currentPath);
                    break;
                case "SourceHamamatsu":
                    DisplaySourceHamamatsu(jsonObject, typeContentPanel, currentPath);
                    break;
                case "StartupDevice":
                    DisplayStartupDevice(jsonObject, typeContentPanel, currentPath);
                    break;

                default:
                    // Default display for unknown typed objects
                    foreach (var property in jsonObject)
                    {
                        string newPath = string.IsNullOrEmpty(currentPath) ? property.Key : $"{currentPath}.{property.Key}";
                        DisplayJsonNode(property.Value, new ItemsControl() { Items = typeContentPanel.Children }, newPath);
                    }
                    break;
            }
        }

        // Example display method for CollisionBox
        private void DisplayCollisionBox(JsonObject collisionBoxObject, StackPanel parentPanel, string currentPath)
        {
            // Display and edit specific properties of CollisionBox
            DisplayJsonProperty(collisionBoxObject, parentPanel, currentPath, "ActorId", isEditable: true);
            DisplayJsonProperty(collisionBoxObject, parentPanel, currentPath, "MinX", isEditable: true);
            DisplayJsonProperty(collisionBoxObject, parentPanel, currentPath, "MaxX", isEditable: true);
            DisplayJsonProperty(collisionBoxObject, parentPanel, currentPath, "MinY", isEditable: true);
            DisplayJsonProperty(collisionBoxObject, parentPanel, currentPath, "MaxY", isEditable: true);
            DisplayJsonProperty(collisionBoxObject, parentPanel, currentPath, "MinZ", isEditable: true);
            DisplayJsonProperty(collisionBoxObject, parentPanel, currentPath, "MaxZ", isEditable: true);
            DisplayJsonProperty(collisionBoxObject, parentPanel, currentPath, "IsStatic", isEditable: true);

            // Recursively display any other properties (if any)
            foreach (var property in collisionBoxObject)
            {
                if (property.Key != "$type" && property.Key != "ActorId" &&
                    property.Key != "MinX" && property.Key != "MaxX" &&
                    property.Key != "MinY" && property.Key != "MaxY" &&
                    property.Key != "MinZ" && property.Key != "MaxZ" &&
                    property.Key != "IsStatic")
                {
                    string newPath = $"{currentPath}.{property.Key}";
                    DisplayJsonNode(property.Value, new ItemsControl() { Items = parentPanel.Children }, newPath); // Pass ItemsControl with StackPanel's children
                }
            }
        }

        // Example display method for StageTest
        private void DisplayStageTest(JsonObject stageTestObject, StackPanel parentPanel, string currentPath)
        {
            DisplayJsonProperty(stageTestObject, parentPanel, currentPath, "DeviceId", isEditable: true);
            // Add other specific properties for StageTest
            DisplayJsonProperty(stageTestObject, parentPanel, currentPath, "SomeStageTestProperty", isEditable: true); // Example

            // Recursively display any other properties
            foreach (var property in stageTestObject)
            {
                if (property.Key != "$type" && property.Key != "DeviceId" && property.Key != "SomeStageTestProperty")
                {
                    string newPath = $"{currentPath}.{property.Key}";
                    DisplayJsonNode(property.Value, new ItemsControl() { Items = parentPanel.Children }, newPath);
                }
            }
        }

        private void DisplayStageBR(JsonObject stageBRObject, StackPanel parentPanel, string currentPath)
        {
            DisplayJsonProperty(stageBRObject, parentPanel, currentPath, "DeviceId", isEditable: true);
            // Add other specific properties for StageBR
            // Example: if StageBR had a property like "BRSetting"
            // DisplayJsonProperty(stageBRObject, parentPanel, currentPath, "BRSetting", isEditable: true);


            foreach (var property in stageBRObject)
            {
                if (property.Key != "$type" && property.Key != "DeviceId")
                {
                    string newPath = $"{currentPath}.{property.Key}";
                    DisplayJsonNode(property.Value, new ItemsControl() { Items = parentPanel.Children }, newPath);
                }
            }
        }

        private void DisplayAxisBR(JsonObject axisBRObject, StackPanel parentPanel, string currentPath)
        {
            DisplayJsonProperty(axisBRObject, parentPanel, currentPath, "DeviceId", isEditable: true);
            DisplayJsonProperty(axisBRObject, parentPanel, currentPath, "ControllerId", isEditable: true);
            DisplayJsonProperty(axisBRObject, parentPanel, currentPath, "AxisNodeId", isEditable: true);
            DisplayJsonProperty(axisBRObject, parentPanel, currentPath, "Movement", isEditable: true);
            DisplayJsonProperty(axisBRObject, parentPanel, currentPath, "MoveAfterInit", isEditable: true);
            DisplayJsonProperty(axisBRObject, parentPanel, currentPath, "AxisName", isEditable: true);
            DisplayJsonProperty(axisBRObject, parentPanel, currentPath, "DefaultVelocity", isEditable: true);
            DisplayJsonProperty(axisBRObject, parentPanel, currentPath, "DefaultAcceleration", isEditable: true);
            DisplayJsonProperty(axisBRObject, parentPanel, currentPath, "LimMin", isEditable: true);
            DisplayJsonProperty(axisBRObject, parentPanel, currentPath, "LimMax", isEditable: true);

            foreach (var property in axisBRObject)
            {
                if (property.Key != "$type" && property.Key != "DeviceId" && property.Key != "ControllerId" &&
                    property.Key != "AxisNodeId" && property.Key != "Movement" && property.Key != "MoveAfterInit" &&
                    property.Key != "AxisName" && property.Key != "DefaultVelocity" && property.Key != "DefaultAcceleration" &&
                    property.Key != "LimMin" && property.Key != "LimMax")
                {
                    string newPath = $"{currentPath}.{property.Key}";
                    DisplayJsonNode(property.Value, new ItemsControl() { Items = parentPanel.Children }, newPath);
                }
            }
        }

        private void DisplayAxisStandard(JsonObject axisStandardObject, StackPanel parentPanel, string currentPath)
        {
            DisplayJsonProperty(axisStandardObject, parentPanel, currentPath, "DeviceId", isEditable: true);
            DisplayJsonProperty(axisStandardObject, parentPanel, currentPath, "ControllerId", isEditable: true);
            DisplayJsonProperty(axisStandardObject, parentPanel, currentPath, "AxisNodeId", isEditable: true);
            DisplayJsonProperty(axisStandardObject, parentPanel, currentPath, "Movement", isEditable: true);
            DisplayJsonProperty(axisStandardObject, parentPanel, currentPath, "MoveAfterInit", isEditable: true);
            DisplayJsonProperty(axisStandardObject, parentPanel, currentPath, "LimMin", isEditable: true);
            DisplayJsonProperty(axisStandardObject, parentPanel, currentPath, "LimMax", isEditable: true);

            foreach (var property in axisStandardObject)
            {
                if (property.Key != "$type" && property.Key != "DeviceId" && property.Key != "ControllerId" &&
                    property.Key != "AxisNodeId" && property.Key != "Movement" && property.Key != "MoveAfterInit" &&
                    property.Key != "LimMin" && property.Key != "LimMax")
                {
                    string newPath = $"{currentPath}.{property.Key}";
                    DisplayJsonNode(property.Value, new ItemsControl() { Items = parentPanel.Children }, newPath);
                }
            }
        }

        private void DisplayDetectorSL(JsonObject detectorSLObject, StackPanel parentPanel, string currentPath)
        {
            DisplayJsonProperty(detectorSLObject, parentPanel, currentPath, "DeviceId", isEditable: true);
            DisplayJsonProperty(detectorSLObject, parentPanel, currentPath, "Flip90ClockW", isEditable: true);

            // Handle nested arrays/objects within DetectorSL (e.g., DetectorModes)
            if (detectorSLObject.TryGetPropertyValue("DetectorModes", out var modesNode) && modesNode is JsonArray modesArray)
            {
                var modesExpander = new Expander() { Header = "Detector Modes", Margin = new Thickness(0, 5, 0, 0) };
                var modesItemsControl = new ItemsControl() { Margin = new Thickness(10, 0, 0, 0) };
                modesExpander.Content = modesItemsControl;
                parentPanel.Children.Add(modesExpander);

                int index = 0;
                foreach (var modeItem in modesArray)
                {
                    string modePath = $"{currentPath}.DetectorModes[{index}]";
                    DisplayJsonNode(modeItem, modesItemsControl, modePath); // Recursively display modes
                    index++;
                }
            }


            foreach (var property in detectorSLObject)
            {
                if (property.Key != "$type" && property.Key != "DeviceId" && property.Key != "Flip90ClockW" && property.Key != "DetectorModes")
                {
                    string newPath = $"{currentPath}.{property.Key}";
                    DisplayJsonNode(property.Value, new ItemsControl() { Items = parentPanel.Children }, newPath);
                }
            }
        }

        private void DisplayTestSource(JsonObject testSourceObject, StackPanel parentPanel, string currentPath)
        {
            DisplayJsonProperty(testSourceObject, parentPanel, currentPath, "DeviceId", isEditable: true);
            DisplayJsonProperty(testSourceObject, parentPanel, currentPath, "MaxCurrent", isEditable: true);

            foreach (var property in testSourceObject)
            {
                if (property.Key != "$type" && property.Key != "DeviceId" && property.Key != "MaxCurrent")
                {
                    string newPath = $"{currentPath}.{property.Key}";
                    DisplayJsonNode(property.Value, new ItemsControl() { Items = parentPanel.Children }, newPath);
                }
            }
        }

        private void DisplaySourceHamamatsu(JsonObject hamamatsuSourceObject, StackPanel parentPanel, string currentPath)
        {
            DisplayJsonProperty(hamamatsuSourceObject, parentPanel, currentPath, "DeviceId", isEditable: true);
            // Add other specific properties for SourceHamamatsu
            // Example: DisplayJsonProperty(hamamatsuSourceObject, parentPanel, currentPath, "HamamatsuSetting", isEditable: true);


            foreach (var property in hamamatsuSourceObject)
            {
                if (property.Key != "$type" && property.Key != "DeviceId")
                {
                    string newPath = $"{currentPath}.{property.Key}";
                    DisplayJsonNode(property.Value, new ItemsControl() { Items = parentPanel.Children }, newPath);
                }
            }
        }

        private void DisplayStartupDevice(JsonObject startupDeviceObject, StackPanel parentPanel, string currentPath)
        {
            DisplayJsonProperty(startupDeviceObject, parentPanel, currentPath, "DeviceId", isEditable: true);

            foreach (var property in startupDeviceObject)
            {
                if (property.Key != "$type" && property.Key != "DeviceId")
                {
                    string newPath = $"{currentPath}.{property.Key}";
                    DisplayJsonNode(property.Value, new ItemsControl() { Items = parentPanel.Children }, newPath);
                }
            }
        }

        // Event handler for CheckBox checked/unchecked (New)
        private void BooleanCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is string path)
            {
                if (_loadedJsonNode != null)
                {
                    JsonNode targetNode = FindNodeByPath(_loadedJsonNode, path);
                    if (targetNode != null && targetNode.GetValueKind() == JsonValueKind.True || targetNode.GetValueKind() == JsonValueKind.False)
                    {
                        try
                        {
                            // Update the JsonNode value based on CheckBox state
                            bool newValue = checkBox.IsChecked ?? false; // Default to false if IsChecked is null
                            ReplaceNode(_loadedJsonNode, path, JsonValue.Create(newValue));
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error updating boolean value for path {path}: {ex.Message}");
                            ShowError($"Error updating boolean value for '{GetPathSegment(path)}': {ex.Message}");
                            // Revert CheckBox state on error? This might be tricky with async handlers
                        }
                    }
                }
            }
        }

        // Event handler for Pick File button (New)
        private async void PickFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string path)
            {
                var openPicker = new FileOpenPicker();
                openPicker.ViewMode = PickerViewMode.Thumbnail;
                openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                // You might want to set specific file type filters based on the property name or context
                openPicker.FileTypeFilter.Add("*"); // Allow all files for now

                StorageFile file = await openPicker.PickSingleFileAsync();

                if (file != null)
                {
                    // Update the corresponding TextBox with the selected file path
                    // Find the TextBox associated with this button
                    if (button.Parent is StackPanel parentStackPanel)
                    {
                        foreach (var child in parentStackPanel.Children)
                        {
                            if (child is TextBox pathTextBox && pathTextBox.Tag is string textBoxPath && textBoxPath == path)
                            {
                                pathTextBox.Text = file.Path;
                                // Trigger the LostFocus event of the TextBox to update the JsonNode
                                ValueTextBox_LostFocus(pathTextBox, null); // Pass null for RoutedEventArgs for simplicity
                                break;
                            }
                        }
                    }
                }
            }
        }

        // Helper method to display a single JSON property
        private void DisplayJsonProperty(JsonObject parentObject, StackPanel parentPanel, string parentPath, string propertyName, bool isEditable, JsonValue propertyValueNode = null)
        {
            if (parentObject == null) return; // Handle cases where parentObject is null

            if (parentObject.TryGetPropertyValue(propertyName, out var propertyNode))
            {
                var propertyStackPanel = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
                propertyStackPanel.Children.Add(new TextBlock() { Text = $"{propertyName}:", FontWeight = Microsoft.UI.Text.FontWeights.Bold, Width = 150 });

                string propertyPath = $"{parentPath}.{propertyName}";

                if (isEditable)
                {
                    // Use different controls based on ValueKind or property name
                    switch (propertyNode.GetValueKind())
                    {
                        case JsonValueKind.String:
                            // Check if it's a known path property (e.g., file path, DLL path)
                            if (propertyName.EndsWith("Path") || propertyName.EndsWith("Assembly")) // Basic check
                            {
                                // Could add a button to open a file picker
                                var pathTextBox = new TextBox() { Text = propertyNode.ToJsonString().Trim('"'), MinWidth = 200 }; // Trim quotes for file paths
                                pathTextBox.Tag = propertyPath;
                                pathTextBox.LostFocus += ValueTextBox_LostFocus; // Use same handler
                                propertyStackPanel.Children.Add(pathTextBox);
                                // Add a file picker button
                                var pickFileButton = new Button() { Content = "...", Tag = propertyPath, FontSize = 10, Padding = new Thickness(5, 0, 5, 0) };
                                pickFileButton.Click += PickFileButton_Click;
                                propertyStackPanel.Children.Add(pickFileButton);

                            }
                            else
                            {
                                var valueTextBox = new TextBox() { Text = propertyNode.ToJsonString().Trim('"'), MinWidth = 200 }; // Trim quotes
                                valueTextBox.Tag = propertyPath;
                                valueTextBox.LostFocus += ValueTextBox_LostFocus;
                                propertyStackPanel.Children.Add(valueTextBox);
                            }
                            break;
                        case JsonValueKind.Number:
                            var numberTextBox = new TextBox() { Text = propertyNode.ToJsonString(), MinWidth = 200 };
                            numberTextBox.Tag = propertyPath;
                            numberTextBox.LostFocus += ValueTextBox_LostFocus;
                            // Add input validation for numbers (e.g., only allow digits and decimal point)
                            numberTextBox.InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } };
                            propertyStackPanel.Children.Add(numberTextBox);
                            break;
                        case JsonValueKind.True:
                        case JsonValueKind.False:
                            var booleanCheckBox = new CheckBox() { IsChecked = propertyNode.GetValue<bool>() };
                            booleanCheckBox.Tag = propertyPath;
                            booleanCheckBox.Checked += BooleanCheckBox_CheckedChanged;
                            booleanCheckBox.Unchecked += BooleanCheckBox_CheckedChanged;
                            propertyStackPanel.Children.Add(booleanCheckBox);
                            break;
                        case JsonValueKind.Null:
                            var nullTextBox = new TextBox() { Text = "null", IsReadOnly = true, MinWidth = 200 }; // Display null as text
                            nullTextBox.Tag = propertyPath;
                            // Allow setting to non-null? This is complex and might require a dialog
                            // For now, keep as read-only representation of null
                            propertyStackPanel.Children.Add(nullTextBox);
                            break;
                        default:
                            // For other value kinds (Object, Array), display as read-only string representation
                            propertyStackPanel.Children.Add(new TextBlock() { Text = propertyNode.ToJsonString() });
                            break;
                    }
                }
                else
                {
                    propertyStackPanel.Children.Add(new TextBlock() { Text = propertyNode.ToJsonString() });
                }

                parentPanel.Children.Add(propertyStackPanel);
            }
            // Optional: Add a way to indicate missing properties if they are expected by the schema
        }

        // Helper to get the last segment of a JSON path
        private string GetPathSegment(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            int lastDot = path.LastIndexOf('.');
            int lastBracket = path.LastIndexOf(']');

            if (lastBracket > lastDot) // Path ends with array index
            {
                int openBracket = path.LastIndexOf('[');
                if (openBracket > -1)
                {
                    return path.Substring(openBracket); // Return [index]
                }
            }
            else if (lastDot > -1) // Path ends with object property
            {
                return path.Substring(lastDot + 1); // Return PropertyName
            }
            else // Simple property name at the root
            {
                return path;
            }
            return null; // Should not happen with valid paths
        }


        private void ValueTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is string path)
            {
                string newValueString = textBox.Text;

                if (_loadedJsonNode != null)
                {
                    JsonNode targetNode = FindNodeByPath(_loadedJsonNode, path);

                    if (targetNode != null)
                    {
                        try
                        {
                            JsonValueKind originalKind = targetNode.GetValueKind();
                            JsonNode newNode = null;

                            switch (originalKind)
                            {
                                case JsonValueKind.String:
                                    newNode = JsonValue.Create(newValueString);
                                    break;
                                case JsonValueKind.Number:
                                    if (double.TryParse(newValueString, out double doubleValue))
                                    {
                                        newNode = JsonValue.Create(doubleValue);
                                    }
                                    else if (long.TryParse(newValueString, out long longValue))
                                    {
                                        newNode = JsonValue.Create(longValue);
                                    }
                                    else
                                    {
                                        ShowError($"Invalid number format for '{GetPathSegment(path)}'");
                                        textBox.Text = targetNode.ToJsonString();
                                        return;
                                    }
                                    break;
                                case JsonValueKind.True:
                                case JsonValueKind.False:
                                    // Handle boolean through CheckBox, but keep this for consistency/fallback
                                    if (bool.TryParse(newValueString, out bool boolValue))
                                    {
                                        newNode = JsonValue.Create(boolValue);
                                    }
                                    else
                                    {
                                        ShowError($"Invalid boolean format for '{GetPathSegment(path)}'. Use CheckBox to edit.");
                                        textBox.Text = targetNode.ToJsonString();
                                        return;
                                    }
                                    break;
                                case JsonValueKind.Null:
                                    if (string.IsNullOrEmpty(newValueString))
                                    {
                                        newNode = JsonValue.Create(null);
                                    }
                                    else
                                    {
                                        ShowError($"Cannot set a non-empty value for a null property '{GetPathSegment(path)}'");
                                        textBox.Text = targetNode.ToJsonString();
                                        return;
                                    }
                                    break;
                                default:
                                    ShowError($"Editing of value kind '{originalKind}' for property '{GetPathSegment(path)}' is not supported.");
                                    textBox.Text = targetNode.ToJsonString();
                                    return;
                            }

                            if (newNode != null)
                            {
                                ReplaceNode(_loadedJsonNode, path, newNode);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error updating JsonNode for path {path}: {ex.Message}");
                            ShowError($"Error updating value for '{GetPathSegment(path)}': {ex.Message}");
                            if (targetNode != null)
                            {
                                textBox.Text = targetNode.ToJsonString();
                            }
                        }
                    }
                }
            }
        }

        // Helper to find a JsonNode by path (improved)
        private JsonNode FindNodeByPath(JsonNode root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;

            JsonNode currentNode = root;
            var segments = Regex.Matches(path, @"\.?([^.\[\]]+)|\[(\d+)\]")
                                .Cast<Match>()
                                .Select(m => m.Value.StartsWith("[") ? m.Groups[2].Value : m.Groups[1].Value)
                                .ToList();


            foreach (var segment in segments)
            {
                if (currentNode is JsonObject jsonObject)
                {
                    if (jsonObject.ContainsKey(segment))
                    {
                        currentNode = jsonObject[segment];
                    }
                    else
                    {
                        return null;
                    }
                }
                else if (currentNode is JsonArray jsonArray)
                {
                    if (int.TryParse(segment, out int index))
                    {
                        if (index >= 0 && index < jsonArray.Count)
                        {
                            currentNode = jsonArray[index];
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }

                if (currentNode == null) return null;
            }

            return currentNode;
        }

        // Helper to replace a JsonNode at a given path (improved)
        private void ReplaceNode(JsonNode root, string path, JsonNode newNode)
        {
            if (root == null || string.IsNullOrEmpty(path) || newNode == null) return;

            string parentPath = GetParentPath(path);
            string lastSegment = GetPathSegment(path);

            if (string.IsNullOrEmpty(parentPath))
            {
                if (root is JsonObject rootObject && !string.IsNullOrEmpty(lastSegment))
                {
                    if (rootObject.ContainsKey(lastSegment))
                    {
                        rootObject[lastSegment] = newNode;
                    }
                }
            }
            else
            {
                JsonNode parentNode = FindNodeByPath(root, parentPath);

                if (parentNode != null)
                {
                    if (parentNode is JsonObject jsonObject && !string.IsNullOrEmpty(lastSegment))
                    {
                        if (jsonObject.ContainsKey(lastSegment))
                        {
                            jsonObject[lastSegment] = newNode;
                        }
                    }
                    else if (parentNode is JsonArray jsonArray && !string.IsNullOrEmpty(lastSegment) && lastSegment.StartsWith("[") && lastSegment.EndsWith("]"))
                    {
                        string indexString = lastSegment.TrimStart('[').TrimEnd(']');
                        if (int.TryParse(indexString, out int index))
                        {
                            if (index >= 0 && index < jsonArray.Count)
                            {
                                jsonArray[index] = newNode;
                            }
                        }
                    }
                }
            }
        }

        // Helper to get the parent path of a JSON path (improved)
        private string GetParentPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            int lastDot = path.LastIndexOf('.');
            int lastBracket = path.LastIndexOf('[');

            if (lastBracket > lastDot)
            {
                int openBracket = path.LastIndexOf('[');
                if (openBracket > -1)
                {
                    return path.Substring(0, openBracket);
                }
            }
            else if (lastDot > -1)
            {
                return path.Substring(0, lastDot);
            }
            return null;
        }

        // Simple helper to show an error message
        private async void ShowError(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = message,
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
        }

        // Event handler for Add Property button
        private async void AddPropertyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string parentPath)
            {
                // Find the parent object node
                JsonNode parentNode = FindNodeByPath(_loadedJsonNode, parentPath);

                if (parentNode is JsonObject jsonObject)
                {
                    // Prompt user for the new property name
                    var inputDialog = new ContentDialog
                    {
                        Title = "Add New Property",
                        Content = new TextBox() { PlaceholderText = "Property Name" },
                        PrimaryButtonText = "Add",
                        CloseButtonText = "Cancel"
                    };

                    var result = await inputDialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        if (inputDialog.Content is TextBox propertyNameTextBox)
                        {
                            string propertyName = propertyNameTextBox.Text.Trim();
                            if (!string.IsNullOrEmpty(propertyName) && !jsonObject.ContainsKey(propertyName))
                            {
                                // Add a new property with a default null value
                                jsonObject.Add(propertyName, null); // Or JsonValue.Create("") for string

                                // Refresh the UI to show the new property
                                RefreshUI(); // We need a method to refresh the UI
                            }
                            else if (jsonObject.ContainsKey(propertyName))
                            {
                                ShowError($"Property '{propertyName}' already exists.");
                            }
                            else
                            {
                                ShowError("Property name cannot be empty.");
                            }
                        }
                    }
                }
            }
        }



        // Event handler for Add Item button (MODIFIED)
        private async void AddItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string parentPath)
            {
                JsonNode parentNode = FindNodeByPath(_loadedJsonNode, parentPath);

                if (parentNode is JsonArray jsonArray)
                {
                    // Determine the expected item types for this array
                    string arrayPathWithoutIndex = Regex.Replace(parentPath, @"\[\d+\]", "[]"); // Normalize path for lookup
                    List<string> possibleTypes = _expectedArrayItemTypes.ContainsKey(arrayPathWithoutIndex)
                        ? _expectedArrayItemTypes[arrayPathWithoutIndex]
                        : null;

                    if (possibleTypes != null && possibleTypes.Any())
                    {
                        // Show a dialog to select the type
                        var typeSelectionDialog = new ContentDialog
                        {
                            Title = "Select Item Type",
                            Content = new ListView() { ItemsSource = possibleTypes },
                            PrimaryButtonText = "Add",
                            CloseButtonText = "Cancel"
                        };

                        var result = await typeSelectionDialog.ShowAsync();

                        if (result == ContentDialogResult.Primary)
                        {
                            if (typeSelectionDialog.Content is ListView typeListView && typeListView.SelectedItem is string selectedType)
                            {
                                // Create a new object based on the selected type
                                JsonObject newItem = CreateDefaultObjectForType(selectedType);

                                if (newItem != null)
                                {
                                    jsonArray.Add(newItem);
                                    RefreshUI();
                                }
                                else
                                {
                                    ShowError($"Could not create a default object for type '{selectedType}'.");
                                }
                            }
                        }
                    }
                    else
                    {
                        // If no specific types are defined, add a default simple value or object
                        jsonArray.Add(null); // Or new JsonObject()
                        RefreshUI();
                    }
                }
            }
        }

        // Helper to create a default JsonObject for a given type (Same as before, needs expansion)
        private JsonObject CreateDefaultObjectForType(string type)
        {
            JsonObject newObject = new JsonObject();
            newObject.Add("$type", type);

            switch (type)
            {
                case "CollisionBox":
                    newObject.Add("ActorId", "NewCollisionBox");
                    newObject.Add("MinX", 0.0);
                    newObject.Add("MaxX", 0.0);
                    newObject.Add("MinY", 0.0);
                    newObject.Add("MaxY", 0.0);
                    newObject.Add("MinZ", 0.0);
                    newObject.Add("MaxZ", 0.0);
                    newObject.Add("IsStatic", false);
                    break;
                case "StageTest":
                    newObject.Add("DeviceId", "NewStageTest");
                    newObject.Add("SomeStageTestProperty", "DefaultValue"); // Example property
                    break;
                case "StageBR":
                    newObject.Add("DeviceId", "NewStageBR");
                    // Add other default properties for StageBR
                    break;
                case "AxisBR":
                    newObject.Add("DeviceId", "NewAxisBR");
                    newObject.Add("ControllerId", "");
                    newObject.Add("AxisNodeId", "");
                    // Add other default properties for AxisBR
                    break;
                case "AxisStandard":
                    newObject.Add("DeviceId", "NewAxisStandard");
                    newObject.Add("ControllerId", "");
                    newObject.Add("AxisNodeId", "");
                    // Add other default properties for AxisStandard
                    break;
                case "DetectorSL":
                    newObject.Add("DeviceId", "NewDetectorSL");
                    // Add other default properties for DetectorSL
                    break;
                case "TestSource":
                    newObject.Add("DeviceId", "NewTestSource");
                    // Add other default properties for TestSource
                    break;
                case "SourceHamamatsu":
                    newObject.Add("DeviceId", "NewSourceHamamatsu");
                    // Add other default properties for SourceHamamatsu
                    break;
                case "StartupDevice":
                    newObject.Add("DeviceId", "NewStartupDevice");
                    break;
                default:
                    return null;
            }

            return newObject;
        }


        // Event handler for Delete button
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string pathToDelete)
            {
                string parentPath = GetParentPath(pathToDelete);
                string lastSegment = GetPathSegment(pathToDelete);

                if (!string.IsNullOrEmpty(parentPath))
                {
                    JsonNode parentNode = FindNodeByPath(_loadedJsonNode, parentPath);

                    if (parentNode != null)
                    {
                        if (parentNode is JsonObject jsonObject && !string.IsNullOrEmpty(lastSegment))
                        {
                            jsonObject.Remove(lastSegment); // Remove property from object
                            RefreshUI(); // Refresh the UI
                        }
                        else if (parentNode is JsonArray jsonArray && !string.IsNullOrEmpty(lastSegment) && lastSegment.StartsWith("[") && lastSegment.EndsWith("]"))
                        {
                            string indexString = lastSegment.TrimStart('[').TrimEnd(']');
                            if (int.TryParse(indexString, out int index))
                            {
                                if (index >= 0 && index < jsonArray.Count)
                                {
                                    jsonArray.RemoveAt(index); // Remove item from array
                                    RefreshUI(); // Refresh the UI
                                }
                            }
                        }
                    }
                }
                else // Deleting a root property
                {
                    if (_loadedJsonNode is JsonObject rootObject && !string.IsNullOrEmpty(lastSegment))
                    {
                        rootObject.Remove(lastSegment);
                        RefreshUI(); // Refresh the UI
                    }
                }
            }
        }

        // Helper method to refresh the entire UI
        private void RefreshUI()
        {
            JsonTreeItemsControl.Items.Clear();
            DisplayJsonNode(_loadedJsonNode, JsonTreeItemsControl, "");
        }


        private async void SaveJsonFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_loadedJsonNode != null && _currentFile != null)
            {
                try
                {
                    string jsonContent = _loadedJsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                    await FileIO.WriteTextAsync(_currentFile, jsonContent);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving JSON: {ex.Message}");
                    ShowError($"Error saving file: {ex.Message}");
                }
            }
        }
    }
}
