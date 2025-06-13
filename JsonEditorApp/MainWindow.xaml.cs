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
                    // Check for $type field for special handling
                    if (node.AsObject().TryGetPropertyValue("$type", out var typeNode) && typeNode.GetValueKind() == JsonValueKind.String)
                    {
                        string type = typeNode.GetValue<string>();
                        DisplayTypedObject(node.AsObject(), parentItemsControl, currentPath, type);
                    }
                    else
                    {
                        // Default handling for objects without $type
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
                    var valueStackPanel = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
                    string propertyName = GetPathSegment(currentPath);
                    if (!string.IsNullOrEmpty(propertyName))
                    {
                        valueStackPanel.Children.Add(new TextBlock() { Text = $"{propertyName}:", FontWeight = Microsoft.UI.Text.FontWeights.Bold, Width = 150 });
                    }

                    var valueTextBox = new TextBox() { Text = node.ToJsonString(), MinWidth = 200 };
                    valueTextBox.Tag = currentPath;
                    valueTextBox.LostFocus += ValueTextBox_LostFocus;
                    valueStackPanel.Children.Add(valueTextBox);

                    var deleteButton = new Button() { Content = "x", Tag = currentPath, FontSize = 10, Padding = new Thickness(5, 0, 5, 0) };
                    deleteButton.Click += DeleteButton_Click;
                    valueStackPanel.Children.Add(deleteButton);

                    if (parentItemsControl != null)
                    {
                        parentItemsControl.Items.Add(valueStackPanel);
                    }

                    break;
            }
        }

        // New method to display objects with a $type field
        private void DisplayTypedObject(JsonObject jsonObject, ItemsControl parentItemsControl, string currentPath, string type)
        {
            var typeExpander = new Expander();
            var typeHeaderStackPanel = new StackPanel() { Orientation = Orientation.Horizontal, Spacing = 10 };
            typeHeaderStackPanel.Children.Add(new TextBlock() { Text = $"{GetPathSegment(currentPath) ?? "Item"} ({type})", FontWeight = Microsoft.UI.Text.FontWeights.Bold }); // Show type in header

            // Add delete button for typed object
            var deleteButton = new Button() { Content = "x", Tag = currentPath, FontSize = 10, Padding = new Thickness(5, 0, 5, 0) };
            deleteButton.Click += DeleteButton_Click; // Use the same delete handler
            typeHeaderStackPanel.Children.Add(deleteButton);


            typeExpander.Header = typeHeaderStackPanel;

            var typeItemsControl = new ItemsControl() { Margin = new Thickness(10, 0, 0, 0) };
            typeExpander.Content = typeItemsControl;

            if (parentItemsControl != null)
            {
                parentItemsControl.Items.Add(typeExpander);
            }

            // Display properties of the typed object
            foreach (var property in jsonObject)
            {
                string newPath = string.IsNullOrEmpty(currentPath) ? property.Key : $"{currentPath}.{property.Key}";
                DisplayJsonNode(property.Value, typeItemsControl, newPath); // Recursively display child nodes
            }
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
                                        return; // Stop processing on error
                                    }
                                    break;
                                case JsonValueKind.True:
                                case JsonValueKind.False:
                                    if (bool.TryParse(newValueString, out bool boolValue))
                                    {
                                        newNode = JsonValue.Create(boolValue);
                                    }
                                    else
                                    {
                                        ShowError($"Invalid boolean format for '{GetPathSegment(path)}'");
                                        textBox.Text = targetNode.ToJsonString();
                                        return; // Stop processing on error
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
                                        return; // Stop processing on error
                                    }
                                    break;
                                default:
                                    ShowError($"Editing of value kind '{originalKind}' for property '{GetPathSegment(path)}' is not supported.");
                                    textBox.Text = targetNode.ToJsonString();
                                    return; // Stop processing on unsupported kind
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

        // Helper to create a default JsonObject for a given type (basic implementation)
        private JsonObject CreateDefaultObjectForType(string type)
        {
            JsonObject newObject = new JsonObject();
            newObject.Add("$type", type); // Add the type field

            // Add some basic default properties based on the type (you'll need to expand this)
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
                    // Add other default properties for StageTest
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
                case "StartupDevice": // Generic type for Startup array
                    newObject.Add("DeviceId", "NewStartupDevice");
                    break;
                default:
                    // Handle unknown types - maybe return null or a basic object
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
