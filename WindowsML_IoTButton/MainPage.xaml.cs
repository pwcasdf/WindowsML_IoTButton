using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Media.Capture;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using System.Diagnostics;
using Windows.System.Display;
using Windows.Media.MediaProperties;
using Windows.Graphics.Display;
using Windows.Storage.FileProperties;
using Windows.Devices.Sensors;

using WindowsML_IoTButton.jackIoTLib;
using Microsoft.Azure.Devices.Client;

using Windows.Data.Json;

using Windows.Media.Capture.Frames;
using System.Threading;
using Windows.UI.Core;
using Windows.Storage;
using System.Runtime.CompilerServices;
using System.ComponentModel;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace WindowsML_IoTButton
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MediaCapture mediaCapture;
        private bool isInitialized;
        private bool externalCamera;
        private bool mirroringPreview;
        private readonly DisplayRequest displayRequest = new DisplayRequest();
        private IMediaEncodingProperties previewProperties;
        private static readonly Guid RotationKey = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");

        // for rotating helpers    @pwcasdf
        private SimpleOrientation deviceOrientation = SimpleOrientation.NotRotated;
        private DisplayOrientations displayOrientation = DisplayOrientations.Portrait;
        private readonly DisplayInformation displayInformation = DisplayInformation.GetForCurrentView();
        bool reverseCamera = false;  // set if camera preview needs reverse streaming    @pwcadsf

        IotHub hub = new IotHub();
        DeviceClient _DeviceClient = DeviceClient.Create("IoTHub6.azure-devices.net", new DeviceAuthenticationWithRegistrySymmetricKey("", ""), TransportType.Mqtt);

        string Msg_Type = "From_Camera", SMS = "", Receiver = "", Area_No="14", Button_No="3", Item_Name = "", jsonParse = "";

        MediaFrameReader frameReader;
        int processingFlag;
        private ONNXModel model = null;
        public event PropertyChangedEventHandler PropertyChanged;
        string loss, loss2, loss3, loss4, loss5;

        public MainPage()
        {
            this.InitializeComponent();


            ReceivingMessage();
        }

        #region OnNavigatedTo

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            LoadModelAsync();
            await InitializeCameraAsync();
            CheckStock();
        }

        #endregion

        #region Putting Model

        private async void LoadModelAsync()
        {
            // Load the .onnx file
            var modelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/MLforMart.onnx"));
            // Create the model from the file
            // IMPORTANT: Change `Model.CreateModel` to match the class and methods in the
            //   .cs file generated from the ONNX model
            model = await ONNXModel.CreateONNXModel(modelFile);
        }

        #endregion

        private async void CheckStock()
        {
            while (true)
            {
                await Task.Delay(300000); // 300 secs

                Item_Name = "";

                if (float.Parse(loss) >= 75 || float.Parse(loss2) >= 75 || float.Parse(loss3) >= 75 || float.Parse(loss4) >= 75)
                {
                    SMS = "yes";
                    Receiver = "fillup";

                    if (float.Parse(loss) >= 75)
                        Item_Name += "An Sung Tang Myun";

                    if (float.Parse(loss2) >= 75)
                    {
                        if (Item_Name == "")
                            Item_Name += "Cham Gge Ra Myun";
                        else
                            Item_Name += ", Cham Gge Ra Myun";
                    }

                    if (float.Parse(loss3) >= 75)
                    {
                        if (Item_Name == "")
                            Item_Name += "Mi Yuck Guk Ra Myun";
                        else
                            Item_Name += ", Mi Yuck Guk Ra Myun";
                    }

                    if (float.Parse(loss4) >= 75)
                    {
                        if (Item_Name == "")
                            Item_Name += "Shin Ra Myun";
                        else
                            Item_Name += ", Shin Ra Myun";
                    }
                }
                else
                {
                    SMS = "yes";
                    Receiver = "consultant";
                }

                jsonParse = "{\"Msg_Type\": \"" + Msg_Type + "\",\"SMS\": \"" + SMS + "\",\"Receiver\": \"" + Receiver + "\",\"Area_No\": \"" + Area_No + "\",\"Button_No\": \"" + Button_No + "\",\"Item_Name\": \"" + Item_Name + "\"}";

                outgoingTB.Text = jsonParse;

                hub.SendMsgToHub(_DeviceClient, jsonParse);
            }
        }

        #region Camera frame work

        private async Task InitializeCameraAsync()
        {
            if (mediaCapture == null)
            {
                var cameraDevice = await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Front);

                mediaCapture = new MediaCapture();

                var settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id };

                // Initialize MediaCapture
                try
                {
                    await mediaCapture.InitializeAsync(settings);
                    isInitialized = true;
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine("The app was denied access to the camera");
                }

                // If initialization succeeded, start the preview
                if (isInitialized)
                {
                    // Figure out where the camera is located
                    if (cameraDevice.EnclosureLocation == null || cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Unknown)
                    {
                        // No information on the location of the camera, assume it's an external camera, not integrated on the device
                        externalCamera = true;
                    }
                    else
                    {
                        // Camera is fixed on the device
                        externalCamera = false;

                        // Only mirror the preview if the camera is on the front panel
                        mirroringPreview = (cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
                    }

                    // Set up the FrameReader to capture frames from the camera video
                    var frameSource = this.mediaCapture.FrameSources.Where(
                        source => source.Value.Info.SourceKind == MediaFrameSourceKind.Color)
                        .First();
                    this.frameReader =
                        await this.mediaCapture.CreateFrameReaderAsync(frameSource.Value);
                    // Set up handler for frames
                    this.frameReader.FrameArrived += OnFrameArrived;
                    // Start the FrameReader
                    await this.frameReader.StartAsync();

                    await StartPreviewAsync();
                }
            }
        }

        private static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
        {
            // Get available devices for capturing pictures
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Get the desired camera by panel
            DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

            // If there is no device mounted on the desired panel, return the first device found
            return desiredDevice ?? allVideoDevices.FirstOrDefault();
        }

        private async Task StartPreviewAsync()
        {
            // Prevent the device from sleeping while the preview is running
            displayRequest.RequestActive();

            // Set the preview source in the UI and mirror it if necessary
            PreviewControl.Source = mediaCapture;

            PreviewControl.FlowDirection = reverseCamera ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            //PreviewControl.FlowDirection = FlowDirection.RightToLeft;
            //PreviewControl.FlowDirection = mirroringPreview ? FlowDirection.RightToLeft : FlowDirection.RightToLeft;

            // Start the preview
            await mediaCapture.StartPreviewAsync();
            previewProperties = mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);

            // Initialize the preview to the current orientation
            if (previewProperties != null)
            {
                displayOrientation = displayInformation.CurrentOrientation;

                await SetPreviewRotationAsync();
            }
        }

        /// <summary>
        /// Gets the current orientation of the UI in relation to the device (when AutoRotationPreferences cannot be honored) and applies a corrective rotation to the preview
        /// </summary>
        private async Task SetPreviewRotationAsync()
        {
            // Only need to update the orientation if the camera is mounted on the device
            //if (externalCamera) return;

            // Calculate which way and how far to rotate the preview
            int rotationDegrees = ConvertDisplayOrientationToDegrees(displayOrientation);

            // The rotation direction needs to be inverted if the preview is being mirrored
            if (mirroringPreview)
            {
                rotationDegrees = (360 - rotationDegrees) % 360;
            }

            // Add rotation metadata to the preview stream to make sure the aspect ratio / dimensions match when rendering and getting preview frames
            var props = mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
            props.Properties.Add(RotationKey, rotationDegrees);
            await mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);
        }

        async void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            if (Interlocked.CompareExchange(ref this.processingFlag, 1, 0) == 0)
            {
                try
                {
                    using (var frame = sender.TryAcquireLatestFrame())
                    using (var videoFrame = frame.VideoMediaFrame?.GetVideoFrame())
                    {
                        if (videoFrame != null)
                        {
                            // If there is a frame, set it as input to the model
                            ONNXModelInput input = new ONNXModelInput();
                            input.data = videoFrame;
                            // Evaluate the input data
                            var evalOutput = await model.EvaluateAsync(input);

                            // Do something with the model output
                            await this.ProcessOutputAsync(evalOutput);
                        }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref this.processingFlag, 0);
                }
            }
        }

        async Task ProcessOutputAsync(ONNXModelOutput evalOutput)
        {
            // Get the label and loss from the output
            loss = (evalOutput.loss[0]["AnSungEmpty"] * 100.0f).ToString("#0.00");
            loss2 = (evalOutput.loss[0]["ChamGgeEmpty"] * 100.0f).ToString("#0.00");
            loss3 = (evalOutput.loss[0]["MiYuckGukEmpty"] * 100.0f).ToString("#0.00");
            loss4 = (evalOutput.loss[0]["ShinRaMyunEmpty"] * 100.0f).ToString("#0.00");
            loss5 = (evalOutput.loss[0]["AllSet"] * 100.0f).ToString("#0.00");

            // Display the score
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    scoreTB.Text = "An Sung Tang Myun Empty" + "\n   " + loss + "%"
                    + "\nCham Gge Ra Myun Empty" + "\n   " + loss2 +"%"
                    + "\nMi Yuck Guk Ra Myun Empty" + "\n   " + loss3 + "%"
                    + "\nShin Ra Myun Empty" + "\n   " + loss4 + "%"
                    + "\n\n All Ra Myuns are set" + "\n   " + loss5 + "%";
                }
            );
        }

        

        void SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            storage = value;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Rotaion helpers

        /// <summary>
        /// Calculates the current camera orientation from the device orientation by taking into account whether the camera is external or facing the user
        /// </summary>
        /// <returns>The camera orientation in space, with an inverted rotation in the case the camera is mounted on the device and is facing the user</returns>
        private SimpleOrientation GetCameraOrientation()
        {
            if (externalCamera)
            {
                // Cameras that are not attached to the device do not rotate along with it, so apply no rotation
                return SimpleOrientation.NotRotated;
            }

            var result = deviceOrientation;

            // Account for the fact that, on portrait-first devices, the camera sensor is mounted at a 90 degree offset to the native orientation
            if (displayInformation.NativeOrientation == DisplayOrientations.Portrait)
            {
                switch (result)
                {
                    case SimpleOrientation.Rotated90DegreesCounterclockwise:
                        result = SimpleOrientation.NotRotated;
                        break;
                    case SimpleOrientation.Rotated180DegreesCounterclockwise:
                        result = SimpleOrientation.Rotated90DegreesCounterclockwise;
                        break;
                    case SimpleOrientation.Rotated270DegreesCounterclockwise:
                        result = SimpleOrientation.Rotated180DegreesCounterclockwise;
                        break;
                    case SimpleOrientation.NotRotated:
                        result = SimpleOrientation.Rotated270DegreesCounterclockwise;
                        break;
                }
            }

            // If the preview is being mirrored for a front-facing camera, then the rotation should be inverted
            if (mirroringPreview)
            {
                // This only affects the 90 and 270 degree cases, because rotating 0 and 180 degrees is the same clockwise and counter-clockwise
                switch (result)
                {
                    case SimpleOrientation.Rotated90DegreesCounterclockwise:
                        return SimpleOrientation.Rotated270DegreesCounterclockwise;
                    case SimpleOrientation.Rotated270DegreesCounterclockwise:
                        return SimpleOrientation.Rotated90DegreesCounterclockwise;
                }
            }

            return result;
        }

        /// <summary>
        /// Converts the given orientation of the device in space to the corresponding rotation in degrees
        /// </summary>
        /// <param name="orientation">The orientation of the device in space</param>
        /// <returns>An orientation in degrees</returns>
        private static int ConvertDeviceOrientationToDegrees(SimpleOrientation orientation)
        {
            switch (orientation)
            {
                case SimpleOrientation.Rotated90DegreesCounterclockwise:
                    return 90;
                case SimpleOrientation.Rotated180DegreesCounterclockwise:
                    return 180;
                case SimpleOrientation.Rotated270DegreesCounterclockwise:
                    return 270;
                case SimpleOrientation.NotRotated:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Converts the given orientation of the app on the screen to the corresponding rotation in degrees
        /// </summary>
        /// <param name="orientation">The orientation of the app on the screen</param>
        /// <returns>An orientation in degrees</returns>
        private static int ConvertDisplayOrientationToDegrees(DisplayOrientations orientation)
        {
            switch (orientation)
            {
                case DisplayOrientations.Portrait:
                    return 90;
                case DisplayOrientations.LandscapeFlipped:
                    return 180;
                case DisplayOrientations.PortraitFlipped:
                    return 270;
                case DisplayOrientations.Landscape:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Converts the given orientation of the device in space to the metadata that can be added to captured photos
        /// </summary>
        /// <param name="orientation">The orientation of the device in space</param>
        /// <returns></returns>
        private static PhotoOrientation ConvertOrientationToPhotoOrientation(SimpleOrientation orientation)
        {
            switch (orientation)
            {
                case SimpleOrientation.Rotated90DegreesCounterclockwise:
                    return PhotoOrientation.Rotate90;
                case SimpleOrientation.Rotated180DegreesCounterclockwise:
                    return PhotoOrientation.Rotate180;
                case SimpleOrientation.Rotated270DegreesCounterclockwise:
                    return PhotoOrientation.Rotate270;
                case SimpleOrientation.NotRotated:
                default:
                    return PhotoOrientation.Normal;
            }
        }

        #endregion

        #region Receiving messages from cloud
        private async void ReceivingMessage()
        {
            Message ReceivedMessage = new Message();
            string OrderFromHub = null;

            while (true)
            {
                OrderFromHub = null;

                try
                {
                    OrderFromHub = await hub.ReceiveMsgFromHub(_DeviceClient, ReceivedMessage);
                }
                catch
                {
                    Debug.WriteLine("null value occured");
                }
                

                if (OrderFromHub == null)
                    continue;


                try
                {
                    JsonObject ReceivedJSON = JsonObject.Parse(OrderFromHub);

                    incomingTB.Text = OrderFromHub;

                    Area_No = ReceivedJSON.GetObject().GetNamedString("Area_No").ToString();
                    Button_No = ReceivedJSON.GetObject().GetNamedString("Button_No").ToString();

                    Item_Name = "";

                    if (float.Parse(loss) >= 75 || float.Parse(loss2) >= 75 || float.Parse(loss3) >= 75 || float.Parse(loss4) >= 75)
                    {
                        SMS = "yes";
                        Receiver = "fillup";

                        if (float.Parse(loss) >= 75)
                            Item_Name += "An Sung Tang Myun";

                        if (float.Parse(loss2) >= 75)
                        {
                            if (Item_Name == "")
                                Item_Name += "Cham Gge Ra Myun";
                            else
                                Item_Name += ", Cham Gge Ra Myun";
                        }

                        if (float.Parse(loss3) >= 75)
                        {
                            if (Item_Name == "")
                                Item_Name += "Mi Yuck Guk Ra Myun";
                            else
                                Item_Name += ", Mi Yuck Guk Ra Myun";
                        }

                        if (float.Parse(loss4) >= 75)
                        {
                            if (Item_Name == "")
                                Item_Name += "Shin Ra Myun";
                            else
                                Item_Name += ", Shin Ra Myun";
                        }
                    }
                    else
                    {
                        SMS = "yes";
                        Receiver = "consultant";
                    }

                    jsonParse = "{\"Msg_Type\": \"" + Msg_Type + "\",\"SMS\": \"" + SMS + "\",\"Receiver\": \"" + Receiver + "\",\"Area_No\": \"" + Area_No + "\",\"Button_No\": \"" + Button_No + "\",\"Item_Name\": \"" + Item_Name + "\"}";

                    outgoingTB.Text = jsonParse;

                    hub.SendMsgToHub(_DeviceClient, jsonParse);

                }
                catch
                {
                    Debug.WriteLine("while json converting");
                }
            }
        }
        #endregion
    }
}
