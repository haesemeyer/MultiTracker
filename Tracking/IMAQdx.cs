using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable

namespace IMAQdxAPI {
    public unsafe static class IMAQdx {
        const int IMAQDX_MAX_API_STRING_LENGTH = 512;

        public enum IMAQdxError : uint {
            IMAQdxErrorSuccess = 0x0, // Success
            IMAQdxErrorSystemMemoryFull = 0xBFF69000, // Not enough memory
            IMAQdxErrorInternal, // Internal error
            IMAQdxErrorInvalidParameter, // Invalid parameter
            IMAQdxErrorInvalidPointer, // Invalid pointer
            IMAQdxErrorInvalidInterface, // Invalid camera session
            IMAQdxErrorInvalidRegistryKey, // Invalid registry key
            IMAQdxErrorInvalidAddress, // Invalid address
            IMAQdxErrorInvalidDeviceType, // Invalid device type
            IMAQdxErrorNotImplemented, // Not implemented yet
            IMAQdxErrorCameraNotFound, // Camera not found
            IMAQdxErrorCameraInUse, // Camera is already in use.
            IMAQdxErrorCameraNotInitialized, // Camera is not initialized.
            IMAQdxErrorCameraRemoved, // Camera has been removed.
            IMAQdxErrorCameraRunning, // Acquisition in progress.
            IMAQdxErrorCameraNotRunning, // No acquisition in progress.
            IMAQdxErrorAttributeNotSupported, // Attribute not supported by the camera.
            IMAQdxErrorAttributeNotSettable, // Unable to set attribute.
            IMAQdxErrorAttributeNotReadable, // Unable to get attribute.
            IMAQdxErrorAttributeOutOfRange, // Attribute value is out of range.
            IMAQdxErrorBufferNotAvailable, // Requested buffer is unavailable.
            IMAQdxErrorBufferListEmpty, // Buffer list is empty. Add one or more buffers.
            IMAQdxErrorBufferListLocked, // Buffer list is already locked. Reconfigure acquisition and try again.
            IMAQdxErrorBufferListNotLocked, // No buffer list. Reconfigure acquisition and try again.
            IMAQdxErrorResourcesAllocated, // Transfer engine resources already allocated. Reconfigure acquisition and try again.
            IMAQdxErrorResourcesUnavailable, // Insufficient transfer engine resources.
            IMAQdxErrorAsyncWrite, // Unable to perform asychronous register write.
            IMAQdxErrorAsyncRead, // Unable to perform asychronous register read.
            IMAQdxErrorTimeout, // Timeout
            IMAQdxErrorBusReset, // Bus reset occurred during a transaction.
            IMAQdxErrorInvalidXML, // Unable to load camera's XML file.
            IMAQdxErrorFileAccess, // Unable to read/write to file.
            IMAQdxErrorInvalidCameraURLString, // Camera has malformed URL string.
            IMAQdxErrorInvalidCameraFile, // Invalid camera file.
            IMAQdxErrorGenICamError, // Unknown Genicam error.
            IMAQdxErrorFormat7Parameters, // For format 7: The combination of speed, image position, image size, and color coding is incorrect.
            IMAQdxErrorInvalidAttributeType, // The attribute type is not compatible with the passed variable type.
            IMAQdxErrorDLLNotFound, // The DLL could not be found.
            IMAQdxErrorFunctionNotFound, // The function could not be found.
            IMAQdxErrorLicenseNotActivated, // License not activated.
            IMAQdxErrorCameraNotConfiguredForListener, // The camera is not configured properly to support a listener.
            IMAQdxErrorCameraMulticastNotAvailable, // Unable to configure the system for multicast support.
            IMAQdxErrorBufferHasLostPackets, // The requested buffer has lost packets and the user requested an error to be generated.
            IMAQdxErrorGiGEVisionError, // Unknown GiGE Vision error.
            IMAQdxErrorNetworkError, // Unknown network error.
            IMAQdxErrorCameraUnreachable, // Unable to connect to the camera
            IMAQdxErrorHighPerformanceNotSupported, // High performance acquisition is not supported on the specified network interface. Connect the camera to a network interface running the high performance driver.
            IMAQdxErrorInterfaceNotRenamed, // Unable to rename interface. Invalid or duplicate name specified.
            IMAQdxErrorNoSupportedVideoModes, // The camera does not have any video modes which are supported
            IMAQdxErrorSoftwareTriggerOverrun, // Software trigger overrun
            IMAQdxErrorTestPacketNotReceived, // The system did not receive a test packet from the camera. The packet size may be too large for the network configuration or a firewall may be enabled.
            IMAQdxErrorCorruptedImageReceived, // The camera returned a corrupted image
            IMAQdxErrorCameraConfigurationHasChanged, // The camera did not return an image of the correct type it was configured for previously
            IMAQdxErrorCameraInvalidAuthentication, // The camera is configured with password authentication and either the user name and password were not configured or they are incorrect
            IMAQdxErrorUnknownHTTPError, // The camera returned an unknown HTTP error
            IMAQdxErrorKernelDriverUnavailable, // Unable to attach to the kernel mode driver
            IMAQdxErrorGuard = 0xFFFFFFFF
        };

        public enum IMAQdxBusType : uint {
            IMAQdxBusTypeFireWire = 0x31333934,
            IMAQdxBusTypeEthernet = 0x69707634,
            IMAQdxBusTypeSimulator = 0x2073696D,
            IMAQdxBusTypeDirectShow = 0x64736877,
            IMAQdxBusTypeIP = 0x4950636D,
            IMAQdxBusTypeGuard = 0xFFFFFFFF
        };

        public enum IMAQdxCameraControlMode : uint {
            IMAQdxCameraControlModeController,
            IMAQdxCameraControlModeListener,
            IMAQdxCameraControlModeGuard = 0xFFFFFFFF
        };

        public enum IMAQdxBufferNumberMode : uint {
            IMAQdxBufferNumberModeNext,
            IMAQdxBufferNumberModeLast,
            IMAQdxBufferNumberModeBufferNumber,
            IMAQdxBufferNumberModeGuard = 0xFFFFFFFF
        };

        public enum IMAQdxPnpEvent : uint {
            IMAQdxPnpEventCameraAttached,
            IMAQdxPnpEventCameraDetached,
            IMAQdxPnpEventBusReset,
            IMAQdxPnpEventGuard = 0xFFFFFFFF
        };

        public enum IMAQdxBayerPattern : uint {
            IMAQdxBayerPatternNone,
            IMAQdxBayerPatternGB,
            IMAQdxBayerPatternGR,
            IMAQdxBayerPatternBG,
            IMAQdxBayerPatternRG,
            IMAQdxBayerPatternHardware,
            IMAQdxBayerPatternGuard = 0xFFFFFFFF
        };

        public enum IMAQdxDestinationMode : uint {
            IMAQdxDestinationModeUnicast,
            IMAQdxDestinationModeBroadcast,
            IMAQdxDestinationModeMulticast,
            IMAQdxDestinationModeGuard = 0xFFFFFFFF
        };

        public enum IMAQdxAttributeType : uint {
            IMAQdxAttributeTypeU32,
            IMAQdxAttributeTypeI64,
            IMAQdxAttributeTypeF64,
            IMAQdxAttributeTypeString,
            IMAQdxAttributeTypeEnum,
            IMAQdxAttributeTypeBool,
            IMAQdxAttributeTypeCommand,
            IMAQdxAttributeTypeBlob,  //Internal Use Only
            IMAQdxAttributeTypeGuard = 0xFFFFFFFF
        };

        public enum IMAQdxValueType : uint {
            IMAQdxValueTypeU32,
            IMAQdxValueTypeI64,
            IMAQdxValueTypeF64,
            IMAQdxValueTypeString,
            IMAQdxValueTypeEnumItem,
            IMAQdxValueTypeBool,
            IMAQdxValueTypeDisposableString,
            IMAQdxValueTypeGuard = 0xFFFFFFFF
        };

        public enum IMAQdxInterfaceFileFlags : uint {
            IMAQdxInterfaceFileFlagsConnected = 0x1,
            IMAQdxInterfaceFileFlagsDirty = 0x2,
            IMAQdxInterfaceFileFlagsGuard = 0xFFFFFFFF
        };

        public enum IMAQdxOverwriteMode : uint {
            IMAQdxOverwriteModeGetOldest = 0x0,
            IMAQdxOverwriteModeFail = 0x2,
            IMAQdxOverwriteModeGetNewest = 0x3,
            IMAQdxOverwriteModeGuard = 0xFFFFFFFF
        };

        public enum IMAQdxLostPacketMode : uint {
            IMAQdxLostPacketModeIgnore,
            IMAQdxLostPacketModeFail,
            IMAQdxLostPacketModeGuard = 0xFFFFFFFF
        };

        public enum IMAQdxAttributeVisibility : uint {
            IMAQdxAttributeVisibilitySimple = 0x00001000,
            IMAQdxAttributeVisibilityIntermediate = 0x00002000,
            IMAQdxAttributeVisibilityAdvanced = 0x00004000,
            IMAQdxAttributeVisibilityGuard = 0xFFFFFFFF
        };

        public enum IMAQdxStreamChannelMode : uint {
            IMAQdxStreamChannelModeAutomatic,
            IMAQdxStreamChannelModeManual,
            IMAQdxStreamChannelModeGuard = 0xFFFFFFFF
        };

        public enum IMAQdxPixelSignedness : uint {
            IMAQdxPixelSignednessUnsigned,
            IMAQdxPixelSignednessSigned,
            IMAQdxPixelSignednessHardware,
            IMAQdxPixelSignednessGuard = 0xFFFFFFFF
        };

        public struct IMAQdxCameraInformation {
            uint Type;
            uint Version;
            uint Flags;
            uint SerialNumberHi;
            uint SerialNumberLo;
            IMAQdxBusType BusType;
            fixed char InterfaceName[IMAQDX_MAX_API_STRING_LENGTH];
            fixed char VendorName[IMAQDX_MAX_API_STRING_LENGTH];
            fixed char ModelName[IMAQDX_MAX_API_STRING_LENGTH];
            fixed char CameraFileName[IMAQDX_MAX_API_STRING_LENGTH];
            fixed char CameraAttributeURL[IMAQDX_MAX_API_STRING_LENGTH];
        };

        public struct IMAQdxCameraFile {
            uint Type;
            uint Version;
            fixed char FileName[IMAQDX_MAX_API_STRING_LENGTH];
        };

        public struct IMAQdxAttributeInformation {
            IMAQdxAttributeType Type;
            uint Readable;
            uint Writable;
            fixed char Name[IMAQDX_MAX_API_STRING_LENGTH];
        };

        public struct IMAQdxVideoMode {
            uint Value;
            uint Reserved;
            fixed char Name[IMAQDX_MAX_API_STRING_LENGTH];
        };

        public const string IMAQdxAttributeBaseAddress = "CameraInformation::BaseAddress";         // Read only. Gets the base address of the camera registers.
        public const string IMAQdxAttributeBusType = "CameraInformation::BusType";             // Read only. Gets the bus type of the camera.
        public const string IMAQdxAttributeModelName = "CameraInformation::ModelName";           // Read only. Returns the model name.
        public const string IMAQdxAttributeSerialNumberHigh = "CameraInformation::SerialNumberHigh";    // Read only. Gets the upper 32-bits of the camera 64-bit serial number.
        public const string IMAQdxAttributeSerialNumberLow = "CameraInformation::SerialNumberLow";     // Read only. Gets the lower 32-bits of the camera 64-bit serial number.
        public const string IMAQdxAttributeVendorName = "CameraInformation::VendorName";          // Read only. Returns the vendor name.
        public const string IMAQdxAttributeHostIPAddress = "CameraInformation::HostIPAddress";       // Read only. Returns the host adapter IP address.
        public const string IMAQdxAttributeIPAddress = "CameraInformation::IPAddress";           // Read only. Returns the IP address.
        public const string IMAQdxAttributePrimaryURLString = "CameraInformation::PrimaryURLString";    // Read only. Gets the camera's primary URL string.
        public const string IMAQdxAttributeSecondaryURLString = "CameraInformation::SecondaryURLString";  // Read only. Gets the camera's secondary URL string.
        public const string IMAQdxAttributeAcqInProgress = "StatusInformation::AcqInProgress";       // Read only. Gets the current state of the acquisition. TRUE if acquiring; otherwise FALSE.
        public const string IMAQdxAttributeLastBufferCount = "StatusInformation::LastBufferCount";     // Read only. Gets the number of transferred buffers.
        public const string IMAQdxAttributeLastBufferNumber = "StatusInformation::LastBufferNumber";    // Read only. Gets the last cumulative buffer number transferred.
        public const string IMAQdxAttributeLostBufferCount = "StatusInformation::LostBufferCount";     // Read only. Gets the number of lost buffers during an acquisition session.
        public const string IMAQdxAttributeLostPacketCount = "StatusInformation::LostPacketCount";     // Read only. Gets the number of lost packets during an acquisition session.
        public const string IMAQdxAttributeRequestedResendPackets = "StatusInformation::RequestedResendPacketCount"; // Read only. Gets the number of packets requested to be resent during an acquisition session.
        public const string IMAQdxAttributeReceivedResendPackets = "StatusInformation::ReceivedResendPackets"; // Read only. Gets the number of packets that were requested to be resent during an acquisition session and were completed.
        public const string IMAQdxAttributeHandledEventCount = "StatusInformation::HandledEventCount";   // Read only. Gets the number of handled events during an acquisition session.
        public const string IMAQdxAttributeLostEventCount = "StatusInformation::LostEventCount";      // Read only. Gets the number of lost events during an acquisition session.
        public const string IMAQdxAttributeBayerGainB = "AcquisitionAttributes::Bayer::GainB";    // Sets/gets the white balance gain for the blue component of the Bayer conversion.
        public const string IMAQdxAttributeBayerGainG = "AcquisitionAttributes::Bayer::GainG";    // Sets/gets the white balance gain for the green component of the Bayer conversion.
        public const string IMAQdxAttributeBayerGainR = "AcquisitionAttributes::Bayer::GainR";    // Sets/gets the white balance gain for the red component of the Bayer conversion.
        public const string IMAQdxAttributeBayerPattern = "AcquisitionAttributes::Bayer::Pattern";  // Sets/gets the Bayer pattern to use.
        public const string IMAQdxAttributeStreamChannelMode = "AcquisitionAttributes::Controller::StreamChannelMode"; // Gets/sets the mode for allocating a FireWire stream channel.
        public const string IMAQdxAttributeDesiredStreamChannel = "AcquisitionAttributes::Controller::DesiredStreamChannel"; // Gets/sets the stream channel to manually allocate.
        public const string IMAQdxAttributeFrameInterval = "AcquisitionAttributes::FrameInterval";   // Read only. Gets the duration in milliseconds between successive frames.
        public const string IMAQdxAttributeIgnoreFirstFrame = "AcquisitionAttributes::IgnoreFirstFrame"; // Gets/sets the video delay of one frame between starting the camera and receiving the video feed.
        public const string IMAQdxAttributeOffsetX = "OffsetX";                                // Gets/sets the left offset of the image.
        public const string IMAQdxAttributeOffsetY = "OffsetY";                                // Gets/sets the top offset of the image.
        public const string IMAQdxAttributeWidth = "Width";                                  // Gets/sets the width of the image.
        public const string IMAQdxAttributeHeight = "Height";                                 // Gets/sets the height of the image.
        public const string IMAQdxAttributePixelFormat = "PixelFormat";                            // Gets/sets the pixel format of the source sensor.
        public const string IMAQdxAttributePacketSize = "PacketSize";                             // Gets/sets the packet size in bytes.
        public const string IMAQdxAttributePayloadSize = "PayloadSize";                            // Gets/sets the frame size in bytes.
        public const string IMAQdxAttributeSpeed = "AcquisitionAttributes::Speed";           // Gets/sets the transfer speed in Mbps for a FireWire packet.
        public const string IMAQdxAttributeShiftPixelBits = "AcquisitionAttributes::ShiftPixelBits";  // Gets/sets the alignment of 16-bit cameras. Downshift the pixel bits if the camera returns most significant bit-aligned data.
        public const string IMAQdxAttributeSwapPixelBytes = "AcquisitionAttributes::SwapPixelBytes";  // Gets/sets the endianness of 16-bit cameras. Swap the pixel bytes if the camera returns little endian data.
        public const string IMAQdxAttributeOverwriteMode = "AcquisitionAttributes::OverwriteMode";   // Gets/sets the overwrite mode, used to determine acquisition when an image transfer cannot be completed due to an overwritten internal buffer.
        public const string IMAQdxAttributeTimeout = "AcquisitionAttributes::Timeout";         // Gets/sets the timeout value in milliseconds, used to abort an acquisition when the image transfer cannot be completed within the delay.
        public const string IMAQdxAttributeVideoMode = "AcquisitionAttributes::VideoMode";       // Gets/sets the video mode for a camera.
        public const string IMAQdxAttributeBitsPerPixel = "AcquisitionAttributes::BitsPerPixel";    // Gets/sets the actual bits per pixel. For 16-bit components, this represents the actual bit depth (10-, 12-, 14-, or 16-bit).
        public const string IMAQdxAttributePixelSignedness = "AcquisitionAttributes::PixelSignedness"; // Gets/sets the signedness of the pixel. For 16-bit components, this represents the actual pixel signedness (Signed, or Unsigned).
        public const string IMAQdxAttributeReserveDualPackets = "AcquisitionAttributes::ReserveDualPackets"; // Gets/sets if dual packets will be reserved for a very large FireWire packet.
        public const string IMAQdxAttributeReceiveTimestampMode = "AcquisitionAttributes::ReceiveTimestampMode"; // Gets/sets the mode for timestamping images received by the driver.

        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxSnap(uint id, void* image);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxConfigureGrab(uint id);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGrab(uint id, void* image, uint waitForNextBuffer, out uint actualBufferNumber);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxSequence(uint id, void* images, uint count);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxDiscoverEthernetCameras(string address, uint timeout);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxEnumerateCameras(IMAQdxCameraInformation* cameraInformationArray, uint* count, uint connectedOnly);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxResetCamera(string name, uint resetAll);
        /// <summary>
        /// Opens a camera, queries the camera for its capabilities, loads a camera configuration file, and creates a unique reference to the camera.
        /// </summary>
        /// <param name="name">The device name of the camera</param>
        /// <param name="mode">Acquisition mode. If "controller" full control over camers, if "listener" can acquire frame from camera opened elsewhere</param>
        /// <param name="id">The session Id retrieved by the call</param>
        /// <returns>0 if successful or IMAQdx error code</returns>
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxOpenCamera(string name, IMAQdxCameraControlMode mode, out uint id);
        /// <summary>
        /// Stops acquisition, releases resources and closes the session
        /// </summary>
        /// <param name="id">The acquisition session to close</param>
        /// <returns>0 if successful or IMAQdx error code</returns>
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxCloseCamera(uint id);
        /// <summary>
        /// Configures a low-level acquisition previously opened with IMAQdxOpenCamera.
        /// </summary>
        /// <param name="id">The session id</param>
        /// <param name="continuous">Specifies whether the acquisition is continuous (=1) or one-shot (=0, fills whole buffer once).</param>
        /// <param name="bufferCount">For a one-shot acquisition, this parameter specifies the number of images to acquire. For a continuous acquisition, this parameter specifies the number of buffers the driver uses internally</param>
        /// <returns>0 if successful or IMAQdx error code</returns>
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxConfigureAcquisition(uint id, bool continuous, uint bufferCount);
        /// <summary>
        /// Starts an acquisition that was previously configured with IMAQdxConfigureAcquisition
        /// </summary>
        /// <param name="id">The session id</param>
        /// <returns>0 if successful or IMAQdx error code</returns>
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxStartAcquisition(uint id);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGetImage(uint id, void* image, IMAQdxBufferNumberMode mode, uint desiredBufferNumber, uint* actualBufferNumber);
        /// <summary>
        /// Retrieves image data from a camera
        /// </summary>
        /// <param name="id">The session id of the camera</param>
        /// <param name="buffer">The buffer for recieving the image</param>
        /// <param name="bufferSize">The size of the image buffer</param>
        /// <param name="mode">Determines what image to acquire: The next image added to the buffer, the last image added, wait for at least imgage (n)</param>
        /// <param name="desiredBufferNumber">If mode is IMAQdxBufferNumberModeBufferNumber the function waits until at least the image with this number is in the buffer. HOWEVER retrieval does not
        /// delete it from the buffer - whence if the grab loop is lagging the same frame can be reaquired!</param>
        /// <param name="actualBufferNumber">The actual frame number of the retrieved frame</param>
        /// <returns>0 if successful or IMAQdx error code</returns>
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGetImageData(uint id, void* buffer, uint bufferSize, IMAQdxBufferNumberMode mode, uint desiredBufferNumber, out uint actualBufferNumber);
        /// <summary>
        /// Stops an acquisition previously started with IMAQdxStartAcquisition
        /// </summary>
        /// <param name="id">Session on which to stop the acquisition</param>
        /// <returns>0 if successful or IMAQdx error code</returns>
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxStopAcquisition(uint id);
        /// <summary>
        /// Unconfigures an acquisition previously configured with IMAQdxConfigureAcquisition
        /// </summary>
        /// <param name="id">The session which has previously been configured</param>
        /// <returns>0 if successful or IMAQdx error code</returns>
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxUnconfigureAcquisition(uint id);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxEnumerateVideoModes(uint id, IMAQdxVideoMode* videoModeArray, uint* count, uint* currentMode);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxEnumerateAttributes(uint id, IMAQdxAttributeInformation* attributeInformationArray, uint* count, string root);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGetAttribute(uint id, string name, IMAQdxValueType type, void* value);
        /// <summary>
        /// Gets the current value for a camera attribute.
        /// </summary>
        /// <param name="id">The session id</param>
        /// <param name="name">The name of the attribute whose value you want to get. Refer to Attribute Name for a list of attributes.</param>
        /// <param name="type">The type of the value you want to get.</param>
        /// <param name="value">The value of the specified attribute when the function returns.</param>
        /// <returns>0 if successful or IMAQdx error code</returns>
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGetAttribute(uint id, string name, IMAQdxValueType type, out uint value);
        [DllImport("niimaqdx.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IMAQdxError IMAQdxSetAttribute(uint id, string name, IMAQdxValueType type, uint value);
        [DllImport("niimaqdx.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IMAQdxError IMAQdxSetAttribute(uint id, string name, IMAQdxValueType type, long value);
        [DllImport("niimaqdx.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IMAQdxError IMAQdxSetAttribute(uint id, string name, IMAQdxValueType type, double value);
        [DllImport("niimaqdx.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IMAQdxError IMAQdxSetAttribute(uint id, string name, IMAQdxValueType type, string value);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGetAttributeMinimum(uint id, string name, IMAQdxValueType type, void* value);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGetAttributeMaximum(uint id, string name, IMAQdxValueType type, void* value);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGetAttributeIncrement(uint id, string name, IMAQdxValueType type, void* value);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGetAttributeType(uint id, string name, IMAQdxAttributeType* type);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxIsAttributeReadable(uint id, string name, uint* readable);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxIsAttributeWritable(uint id, string name, uint* writable);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxEnumerateAttributeValues(uint id, string name, IMAQdxVideoMode* list, uint* size);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGetAttributeTooltip(uint id, string name, StringBuilder tooltip, uint length);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGetAttributeUnits(uint id, char* name, StringBuilder units, uint length);
        //[DllImport("niimaqdx.dll")] public static extern IMAQdxError IMAQdxRegisterFrameDoneEvent(uint id, uint bufferInterval, FrameDoneEventCallbackPtr callbackFunction, void* callbackData);
        //[DllImport("niimaqdx.dll")] public static extern IMAQdxError IMAQdxRegisterPnpEvent(uint id, IMAQdxPnpEvent event, PnpEventCallbackPtr callbackFunction, void* callbackData);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxWriteRegister(uint id, uint offset, uint value);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxReadRegister(uint id, uint offset, uint* value);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxWriteMemory(uint id, uint offset, byte* values, uint count);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxReadMemory(uint id, uint offset, byte* values, uint count);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGetErrorString(IMAQdxError error, StringBuilder message, uint messageLength);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxWriteAttributes(uint id, string filename);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxReadAttributes(uint id, string filename);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxResetEthernetCameraAddress(string name, string address, string subnet, string gateway, uint timeout);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxEnumerateAttributes2(uint id, IMAQdxAttributeInformation* attributeInformationArray, uint* count, string root, IMAQdxAttributeVisibility visibility);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGetAttributeVisibility(uint id, string name, IMAQdxAttributeVisibility* visibility);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGetAttributeDescription(uint id, string name, StringBuilder description, uint length);
        [DllImport("niimaqdx.dll")]
        public static extern IMAQdxError IMAQdxGetAttributeDisplayName(uint id, string name, StringBuilder displayName, uint length);
        //[DllImport("niimaqdx.dll")] public static extern IMAQdxError IMAQdxRegisterAttributeUpdatedEvent(uint id, char* name, AttributeUpdatedEventCallbackPtr callbackFunction, void* callbackData);
    }
}

#pragma warning disable