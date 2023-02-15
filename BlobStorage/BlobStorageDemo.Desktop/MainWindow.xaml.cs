using Azure.Storage.Blobs;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace BlobStorageDemo.Desktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string connectionString;
        private readonly BlobServiceClient blobServiceClient;

        private readonly Dictionary<string, string[]> blobsDictionary = new();

        public MainWindow()
        {
            InitializeComponent();

            connectionString = Properties.Settings.Default.StorageConnectionString;
            blobServiceClient = new BlobServiceClient(connectionString);
        }

        private async void OnCreateContainerButtonClickAsync(object sender, RoutedEventArgs e)
        {
            //Create a unique name for the container
            string containerName = Guid.NewGuid().ToString();

            // Create the container
            await blobServiceClient.CreateBlobContainerAsync(containerName);
        }

        private async void OnRefreshListsButtonClickAsync(object sender, RoutedEventArgs e)
        {
            // Clear the UI list before adding items to it
            containersList.Items.Clear();
            blobsList.Items.Clear();
            blobsDictionary.Clear();

            // Get all containers and add them to the UI list
            var containers = blobServiceClient.GetBlobContainersAsync();
            await foreach (var container in containers)
            {
                containersList.Items.Add(container.Name);

                // Get reference to a container and list of all its blobs
                var containerClient = blobServiceClient.GetBlobContainerClient(container.Name);
                var blobs = containerClient.GetBlobs().Select(i => i.Name).ToArray();
                blobsDictionary.Add(container.Name, blobs);
            }
        }

        private async void OnUploadButtonClickAsync(object sender, RoutedEventArgs e)
        {
            // Open a file
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }
            var fileName = openFileDialog.SafeFileName;
            var filePath = openFileDialog.FileName;

            // Get a reference to a container
            var selectedContainer = containersList.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedContainer))
            {
                return;
            }
            var containerClient = blobServiceClient.GetBlobContainerClient(selectedContainer);

            // Get a reference to a blob
            var blobClient = containerClient.GetBlobClient(fileName);

            // Upload file to blob storage (overwrite if blob exists)
            await blobClient.UploadAsync(filePath, true);
        }

        private void OnContainerSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            blobsList.Items.Clear();

            var selectedContainer = containersList.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedContainer))
                return;

            foreach (var blob in blobsDictionary[selectedContainer])
            {
                blobsList.Items.Add(blob);
            }
        }

        private async void OnDownloadButtonClickAsync(object sender, RoutedEventArgs e)
        {
            // Get a reference to a container
            var selectedContainer = containersList.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedContainer))
                return;
            var containerClient = blobServiceClient.GetBlobContainerClient(selectedContainer);

            // Get a reference to a blob
            var selectedBlob = blobsList.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedBlob))
                return;
            var blobClient = containerClient.GetBlobClient(selectedBlob);

            // Prompt user for destination file and download the blob
            var saveFileDialog = new SaveFileDialog();
            if (saveFileDialog.ShowDialog() != true)
                return;
            await blobClient.DownloadToAsync(saveFileDialog.FileName);
        }

    }
}