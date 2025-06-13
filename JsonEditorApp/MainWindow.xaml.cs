using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace JsonEditorApp
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }

        private async void OpenJsonFileButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear previous data
            JsonDataPanel.Children.Clear();

            // Configure file picker
            var openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".json");

            // Open the file picker
            StorageFile file = await openPicker.PickSingleFileAsync();

            if (file != null)
            {
                // Read the JSON file
                string jsonContent = await FileIO.ReadTextAsync(file);

                // Deserialize the JSON
                using (JsonDocument document = JsonDocument.Parse(jsonContent))
                {
                    JsonElement root = document.RootElement;
                    DisplayJsonElement(root, JsonDataPanel);
                }
            }
        }

        private void DisplayJsonElement(JsonElement element, StackPanel parentPanel)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        StackPanel propertyPanel = new StackPanel() { Orientation = Orientation.Vertical, Margin = new Thickness(0, 5, 0, 5) };
                        TextBlock nameTextBlock = new TextBlock() { Text = property.Name, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
                        propertyPanel.Children.Add(nameTextBlock);
                        DisplayJsonElement(property.Value, propertyPanel);
                        parentPanel.Children.Add(propertyPanel);
                    }
                    break;
                case JsonValueKind.Array:
                    int index = 0;
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        StackPanel itemPanel = new StackPanel() { Orientation = Orientation.Vertical, Margin = new Thickness(10, 0, 0, 0) };
                        TextBlock indexTextBlock = new TextBlock() { Text = $"[{index}]", FontStyle = Windows.UI.Text.FontStyle.Italic };
                        itemPanel.Children.Add(indexTextBlock);
                        DisplayJsonElement(item, itemPanel);
                        parentPanel.Children.Add(itemPanel);
                        index++;
                    }
                    break;
                case JsonValueKind.String:
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    TextBox valueTextBox = new TextBox() { Text = element.ToString(), IsReadOnly = false }; // Make it editable
                    parentPanel.Children.Add(valueTextBox);
                    break;
            }
        }
    }
}
