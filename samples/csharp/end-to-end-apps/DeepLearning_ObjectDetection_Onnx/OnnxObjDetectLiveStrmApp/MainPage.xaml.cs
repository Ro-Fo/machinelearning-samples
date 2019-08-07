﻿using Microsoft.ML;
using Microsoft.ML.Transforms.Image;
using Microsoft.Toolkit.Uwp.UI.Controls.TextToolbarSymbols;
using OnnxObjectDetectionLiveStreamApp.MLModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using static OnnxObjectDetectionLiveStreamApp.MLModel.OnnxModelConfigurator;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace OnnxObjectDetectionLiveStreamApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private uint fullImageWidth;
        private uint fullImageHeight;
        private int frameCount;
        private MLContext mlContext;
        private ITransformer model;

        public MainPage()
        {
            this.InitializeComponent();

            mlContext = new MLContext();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            GetCameraSize();
            Window.Current.SizeChanged += Current_SizeChanged;

            await CameraPreview.StartAsync();
            CameraPreview.CameraHelper.FrameArrived += CameraFrameArrived;

            var onnxModel = "TinyYolo2_model.onnx";
            StorageFile onnxFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/{onnxModel}"));

            var mlNetModelFile = "TinyYoloModel.zip";
            var storageFolder = ApplicationData.Current.LocalFolder;
            string modelPath = Path.Combine(storageFolder.Path, mlNetModelFile);

            OnnxModelConfigurator onnxModelConfigurator = new OnnxModelConfigurator(onnxFile.Path);
            onnxModelConfigurator.SaveMLNetModel(modelPath);

            model = mlContext.Model.Load(modelPath, out DataViewSchema schema);
        }

        public void DetectObjectsUsingModel(ImageInputData imageInputData)
        {
            IEnumerable<ImageInputData> image = new List<ImageInputData>() { imageInputData };
            IDataView imageDataView = mlContext.Data.LoadFromEnumerable(image);
            var probs = model.Transform(imageDataView); //TODO: Getting results here for video frames; need to store in proper type
        }

        private void GetCameraSize()
        {
            fullImageWidth = (uint)CameraPreview.ActualWidth;
            fullImageHeight = (uint)CameraPreview.ActualHeight;
        }

        private void Current_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            GetCameraSize();
        }

        private async void CameraFrameArrived(object sender, Microsoft.Toolkit.Uwp.Helpers.FrameEventArgs e)
        {
            if (e?.VideoFrame?.SoftwareBitmap == null || 
                model == null) //TODO: Need to do better than this to make sure that the model has been created first
            {
                return;
            }

            SoftwareBitmap softBitmap = SoftwareBitmap.Convert(e.VideoFrame.SoftwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            VideoFrame inputFrame = VideoFrame.CreateWithSoftwareBitmap(softBitmap);

            ImageInputData frame = new ImageInputData();
            DetectObjectsUsingModel(frame);
            
            frameCount++;
            Debug.WriteLine($"Frame received: {frameCount}");

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                DrawOverlays(frameCount);
            });
        }

        private void DrawOverlays(int frameCount)
        {
            CameraCanvas.Children.Clear();
            DrawImageBox(frameCount);
        }

        private void DrawImageBox(int frameCount)
        {
            uint x = 1;
            uint y = 1;
            uint w = 1;
            uint h = 1;

            // TODO: Get the x, y, w, h coordinates from the results of the model to know where the object is detected within the image
            x = fullImageWidth * x / 416;
            y = fullImageHeight * y / 416;
            w = fullImageWidth * w / 416;
            h = fullImageHeight * h / 416;

            var objBox = new Windows.UI.Xaml.Shapes.Rectangle
            {
                Width = w,
                Height = h,
                Fill = new SolidColorBrush(Windows.UI.Colors.Transparent),
                Stroke = new SolidColorBrush(Windows.UI.Colors.Green),
                StrokeThickness = 2.0,
                Margin = new Thickness(x, y, 0, 0)
            };

            var objDescription = new TextBlock
            {
                Margin = new Thickness(x + 4, y + 4, 0, 0),
                Text = $"Test Frame {frameCount}",
                FontWeight = FontWeights.Bold,
                Width = 126,
                Height = 21,
                HorizontalTextAlignment = TextAlignment.Center
            };

            var objDescriptionBackground = new Windows.UI.Xaml.Shapes.Rectangle
            {
                Width = 134,
                Height = 29,
                Fill = new SolidColorBrush(Windows.UI.Colors.Green),
                Margin = new Thickness(x, y, 0, 0)
            };

            CameraCanvas.Children.Add(objDescriptionBackground);
            CameraCanvas.Children.Add(objDescription);
            CameraCanvas.Children.Add(objBox);
        }
    }
}
