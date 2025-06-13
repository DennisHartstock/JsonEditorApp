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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace JsonEditorApp
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private JsonNode _loadedJsonNode; // Use JsonNode to hold the entire JSON structure
        private StorageFile _currentFile; // To keep track of the currently opened file

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

                    // Parse the JSON content into a JsonNode tree
                    _loadedJsonNode = JsonNode.Parse(jsonContent);
                    _currentFile = file; // Store the file for saving

                    // Display the JsonNode tree in the UI
                    DisplayJsonNode(_loadedJsonNode, JsonTreeItemsControl);
                }
                catch (Exception ex)
                {
                    // Handle potential parsing errors
                    System.Diagnostics.Debug.WriteLine($"Error parsing JSON: {ex.Message}");
                    _loadedJsonNode = null; // Clear loaded data
                    JsonTreeItemsControl.ItemsSource = null; // Clear UI
                    _currentFile = null; // Clear file reference
                    // Show error message to user
                }
            }
        }

        private void DisplayJsonNode(JsonNode node, ItemsControl parentItemsControl, string propertyName = null)
        {
            if (node == null) return;

            switch (node.GetValueKind())
            {
                case JsonValueKind.Object:
                    var objectExpander = new Expander();
                    objectExpander.Header = propertyName ?? "Object"; // Use property name as header
                    var objectStackPanel = new StackPanel() { Margin = new Thickness(10, 0, 0, 0) };
                    objectExpander.Content = objectStackPanel;

                    if (parentItemsControl != null)
                    {
                        parentItemsControl.Items.Add(objectExpander);
                    }

                    foreach (var property in node.AsObject())
                    {
                        // Recursively display child nodes
                        DisplayJsonNode(property.Value, new ItemsControl() { ItemsSource = new List<JsonNode>() }, property.Key); // Pass a new ItemsControl for children
                                                                                                                                  // We need to add the child items directly to the objectStackPanel, not a new ItemsControl
                                                                                                                                  // This requires rethinking how we structure the UI hierarchy and pass parent containers
                    }
                    break;
                case JsonValueKind.Array:
                    var arrayExpander = new Expander();
                    arrayExpander.Header = propertyName ?? "Array"; // Use property name as header
                    var arrayItemsControl = new ItemsControl() { Margin = new Thickness(10, 0, 0, 0) };
                    arrayExpander.Content = arrayItemsControl;

                    if (parentItemsControl != null)
                    {
                        parentItemsControl.Items.Add(arrayExpander);
                    }


                    int index = 0;
                    foreach (var item in node.AsArray())
                    {
                        // Recursively display array items
                        DisplayJsonNode(item, new ItemsControl() { ItemsSource = new List<JsonNode>() }, $"[{index}]"); // Use index as header
                                                                                                                        // Similar issue as with objects, need to add to the arrayItemsControl
                        index++;
                    }
                    break;
                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    // Display and edit simple values
                    var valueStackPanel = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
                    if (!string.IsNullOrEmpty(propertyName))
                    {
                        valueStackPanel.Children.Add(new TextBlock() { Text = $"{propertyName}:", FontWeight = Microsoft.UI.Text.FontWeights.Bold, Width = 150 });
                    }
                    var valueTextBox = new TextBox() { Text = node.ToJsonString(), MinWidth = 200 }; // Display value as string
                    // We need a way to bind this TextBox back to the JsonNode value
                    // This requires a custom binding approach or updating the JsonNode manually on TextBox change

                    valueStackPanel.Children.Add(valueTextBox);

                    if (parentItemsControl != null)
                    {
                        parentItemsControl.Items.Add(valueStackPanel);
                    }

                    break;
            }
        }


        private async void SaveJsonFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_loadedJsonNode != null && _currentFile != null)
            {
                try
                {
                    // Serialize the modified JsonNode tree
                    string jsonContent = _loadedJsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

                    await FileIO.WriteTextAsync(_currentFile, jsonContent);
                }
                catch (Exception ex)
                {
                    // Handle potential serialization or file writing errors
                    System.Diagnostics.Debug.WriteLine($"Error saving JSON: {ex.Message}");
                    // Show error message to user
                }
            }
        }
    }
}
