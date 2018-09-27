using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace WPDatabaseWork.Core
{
    public class ImageBlobProcessor
    {
        private ILogger Logger { get; }
        public Context Context { get; }

        public ImageBlobProcessor(Context context)
        {
            Context = context;
            var factory = Context.ServiceProvider.GetService<ILoggerFactory>();
            Logger = factory.CreateLogger<ImageBlobProcessor>();
        }
        public string Process(List<View.File> dataFiles)
        {
            int count = 0;
            double processStart = 30;
            double processStop = 100;
            double processInterval = 1000;

            string status = "";
            /*
            if (dataFiles.Count > 0)
            {
                var container = GetBlobContainer();
                foreach (var dataFile in dataFiles)
                {
                    ProcessFile(dataFile, container);
                    count++;
                    if (count % processInterval == 0)
                    {
                        int processStep = (int)Math.Round((double)(100 * count) / dataFiles.Count);
                        processStart += processStep;
                    }
                }
            }
            */
            status = $"running - All {dataFiles.Count} images uploaded for the blogg";
            return status;
        }
        private void ProcessFile(View.File file, CloudBlobContainer container)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(file.LocalPath);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream inputStream = response.GetResponseStream();
                CloudBlockBlob blob = container.GetBlockBlobReference(file.BlobPath);
                blob.UploadFromStreamAsync(inputStream).Wait();
                Logger.LogDebug($"Uploaded file {file.LocalPath} to {file.BlobPath}");
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed to upload image {file.LocalPath} to the {file.BlobPath}. {e.Message}");
            }
        }

        /*
        private CloudBlobContainer GetBlobContainer()
        {
            var storageUri = new StorageUri(new Uri(Context.Settings.DestinationSite.GetAzureBlobUrl, UriKind.Absolute));
            var credentials = new StorageCredentials(Context.Settings.DestinationSite.GetAzureBlobAccount, Context.Settings.DestinationSite.AzureBlobKey);

            var client = new CloudBlobClient(storageUri, credentials);

            var container = client.GetContainerReference(Context.Settings.DestinationSite.GetBlobContainer());
            container.CreateIfNotExistsAsync().Wait();

            //Default inställning för en container är att det är privat, så vi måste sätta den till blob
            container.SetPermissionsAsync(
                new BlobContainerPermissions
                {
                    PublicAccess = BlobContainerPublicAccessType.Blob
                }).Wait();

            return container;
        }
        */
    }
}
