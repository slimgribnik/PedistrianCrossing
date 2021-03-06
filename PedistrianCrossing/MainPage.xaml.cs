﻿using System;
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
using Windows.Devices.Gpio;    // GPIO header
using Windows.UI.Core;         // DispatcherTime
using Windows.Media.SpeechSynthesis;
using System.Diagnostics;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace PedistrianCrossing
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        bool IsGPIO = true;

        // Light constants
        private const int RED = 0;
        private const int YELLOW = 1;
        private const int GREEN = 2;

        // Traffic light pins
        private int[] TRAFFIC_PINS = { 4, 5, 6 };
        // Button pin
        private const int BUTTON_PIN = 19;
        // Walk light pins
        private int[] WALK_PINS = { 20, 21 };

        // State constants
        ////private const int GREEN_TO_YELLOW = 4;
        ////private const int YELLOW_TO_RED = 8;
        ////private const int WALK_ON = 12;
        ////private const int WALK_WARNING = 22;
        ////private const int WALK_OFF = 30;
        private const int WALK_ON = 2;
        private const int WALK_WARNING = 18;
        private const int GREEN_TO_YELLOW = 26;
        private const int YELLOW_TO_RED = 30;
        //private const int WALK_OFF = GREEN_TO_YELLOW;

       // Traffic light pin variables
        private GpioPin[] Traffic_light = new GpioPin[3];

        // Walk light pin variables
        private GpioPin[] Walk_light = new GpioPin[2];

        // Button pin variable
        private GpioPin Button;

        // Add a Dispatcher Timer
        private DispatcherTimer walkTimer;

        // Variable for counting seconds elapsed
        private int secondsElapsed = 0;

        // On screen display
        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush greenBrush = new SolidColorBrush(Windows.UI.Colors.Green);
        private SolidColorBrush yellowBrush = new SolidColorBrush(Windows.UI.Colors.Yellow);
        private SolidColorBrush grayBrush = new SolidColorBrush(Windows.UI.Colors.LightGray);

        // Some strings to let us know the current state.
        const string WALK_OFF_STR = "Do not walk";
        const string WALK_ON_STR = "Start walking";
        const string WALK_WARNING_STR = "Hurry up";

        // The Windows Speech API interface
        private SpeechSynthesizer synthesizer;

        public MainPage()
        {
            this.InitializeComponent();
            
            InitGPIO();
            InitDisplay();

            // Create a new SpeechSynthesizer instance for later use.
            synthesizer = new SpeechSynthesizer();

            this.secondsElapsed = 0;
        }

        // Setup the display
        private void InitDisplay()
        {
            WalkStatus.Text = "Do not walk";
            LED_T_RED.Fill = redBrush;
            LED_T_YELLOW.Fill = grayBrush;
            LED_T_GREEN.Fill = grayBrush;
            LED_W_RED.Fill = redBrush;
            LED_W_YELLOW.Fill = grayBrush;
        }

        // Setup the GPIO initial states
        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();
            // Do nothing if there is no GPIO controller

            if (gpio == null)
            {
                IsGPIO = false;
                return;
            }

            // Initialize the GPIO pins
            for (int i = 0; i < 3; i++)
            {
                this.Traffic_light[i] = gpio.OpenPin(TRAFFIC_PINS[i]);
                this.Traffic_light[i].SetDriveMode(GpioPinDriveMode.Output);
            }

            this.Button = gpio.OpenPin(BUTTON_PIN);

            for (int i = 0; i < 2; i++)
            {
                this.Walk_light[i] = gpio.OpenPin(WALK_PINS[i]);
                this.Walk_light[i].SetDriveMode(GpioPinDriveMode.Output);
            }

            this.Traffic_light[RED].Write(GpioPinValue.High);
            this.Traffic_light[YELLOW].Write(GpioPinValue.Low);
            this.Traffic_light[GREEN].Write(GpioPinValue.Low);
            this.Walk_light[RED].Write(GpioPinValue.High);
            this.Walk_light[YELLOW].Write(GpioPinValue.Low);

            // Check if input pull-up resistors are supported
            if (this.Button.IsDriveModeSupported(GpioPinDriveMode.InputPullUp))
                this.Button.SetDriveMode(GpioPinDriveMode.InputPullUp);
            else
                this.Button.SetDriveMode(GpioPinDriveMode.Input);
            // Set a debounce timeout to filter out switch bounce noise from a button press
            this.Button.DebounceTimeout = TimeSpan.FromMilliseconds(50);
            // Register for the ValueChanged event so our Button_ValueChanged
            // function is called when the button is pressed
            walkTimer = new DispatcherTimer();
            walkTimer.Interval = TimeSpan.FromMilliseconds(1000);
            walkTimer.Tick += WalkTimer_Tick;
            this.Button.ValueChanged += Button_ValueChanged;
        }

        // Detect button press event
        private void Button_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs e)
        {
            // Pedestrian has pushed the button. Start timer for going red.
            if (e.Edge == GpioPinEdge.FallingEdge)
            {
              // Start the timer if and only if not in a cycle
                if (this.secondsElapsed == 0)
                {
                    // need to invoke UI updates on the UI thread because this event
                    // handler gets invoked on a separate thread.
                    var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        if (e.Edge == GpioPinEdge.FallingEdge)
                        {
                            this.walkTimer.Start();
                        }
                    });
                }
            }
        }

        // Here you do the lights state change if and only if elapsed_seconds > 0
        private async void WalkTimer_Tick(object sender, object e)

        {
            if(secondsElapsed == 0)
            {
               this.WalkStatus.Text = WALK_ON_STR;
               // Use another method to wrap the speech synthesis functionality.
               await TextToSpeech(WALK_ON_STR);
               this.LED_T_GREEN.Fill = greenBrush;
               this.LED_T_RED.Fill = grayBrush;
               this.LED_W_RED.Fill = grayBrush;
               this.LED_W_YELLOW.Fill = yellowBrush;
                if (IsGPIO)
                {
                   this.Traffic_light[GREEN].Write(GpioPinValue.High);
                   this.Traffic_light[RED].Write(GpioPinValue.Low);
                   this.Walk_light[YELLOW].Write(GpioPinValue.High);
                   this.Walk_light[RED].Write(GpioPinValue.Low);
                }
            }

            if ((secondsElapsed >= WALK_WARNING) && (this.secondsElapsed <= GREEN_TO_YELLOW))
            {
                WalkStatus.Text = WALK_WARNING_STR + " : " + (GREEN_TO_YELLOW - secondsElapsed).ToString();
                await TextToSpeech((GREEN_TO_YELLOW - secondsElapsed).ToString());
                // Blink the walk warning light
                if ((secondsElapsed % 2) == 0)
                {
                   this.LED_W_YELLOW.Fill = grayBrush;
                   if (IsGPIO)
                       this.Walk_light[YELLOW].Write(GpioPinValue.Low);
                }
                else
                {
                    this.LED_W_YELLOW.Fill = yellowBrush;
                   if (IsGPIO)
                    this.Walk_light[YELLOW].Write(GpioPinValue.High);
                }
            }

            // Change green to yellow
            if (this.secondsElapsed == GREEN_TO_YELLOW)
            {
                WalkStatus.Text = WALK_OFF_STR;
                await TextToSpeech(WALK_OFF_STR);
                this.LED_T_GREEN.Fill = grayBrush;
                this.LED_T_YELLOW.Fill = yellowBrush;
                this.LED_W_YELLOW.Fill = grayBrush;
                this.LED_W_RED.Fill = redBrush;

                if (IsGPIO)
                {
                    this.Traffic_light[GREEN].Write(GpioPinValue.Low);
                    this.Traffic_light[YELLOW].Write(GpioPinValue.High);
                    this.Walk_light[YELLOW].Write(GpioPinValue.Low);
                    this.Walk_light[RED].Write(GpioPinValue.High);
                }
            }



            if (this.secondsElapsed == YELLOW_TO_RED)
            {
                this.LED_T_YELLOW.Fill = grayBrush;
                this.LED_T_RED.Fill = redBrush;
                if (IsGPIO)
                {
                this.Traffic_light[YELLOW].Write(GpioPinValue.Low);
                this.Traffic_light[RED].Write(GpioPinValue.High);
                }
                this.secondsElapsed = 0;
                this.walkTimer.Stop();
                return;
            }

            // increment the counter
            this.secondsElapsed += 1;
        }

        private async System.Threading.Tasks.Task TextToSpeech(String textToSpeak)
        {
            // Because we are running somewhere other than the UI thread and we need to talk to a UI element (the media control)
            // we need to use the dispatcher to move the calls to the right thread.
            await Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.High,
                async () =>
                {
                    SpeechSynthesisStream synthesisStream;

                    //creating a stream from the text which can be played using media element. This API converts text input into a stream.
                    synthesisStream = await synthesizer.SynthesizeTextToStreamAsync(textToSpeak);

                    // start this audio stream playing
                    media.AutoPlay = true;
                    media.SetSource(synthesisStream, synthesisStream.ContentType);
                    media.Play();
                }
            );
        }

        private void btnStartStop_Click(object sender, RoutedEventArgs e)
        {
            walkTimer = new DispatcherTimer();
            walkTimer.Interval = TimeSpan.FromMilliseconds(1000);
            walkTimer.Tick += WalkTimer_Tick;

            walkTimer.Start();
        }
    }
}
