using Microsoft.MT.Api;
using Microsoft.MT.Api.TestUtils;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace S2SMtDemoClient
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private enum UiState //list of items for use in the code
        {
            GettingLanguageList,
            MissingLanguageList,
            ReadyToConnect,
            Connecting,
            Connected,
            Disconnecting
        }


        private enum MessageKind  //list of items for use throughout the code
        {
            Chat, // Translate mode
            ChatDetect1, // Detect and translate - result for first language
            ChatDetect2, // Detect and translate - result for second language
            Error,
            Status
        }

        private UiState currentState; //create a variable of enum type UiState

        private Dictionary<string, string> spokenLanguages; //create dictionary for the speech translation langauges 
        private Dictionary<string, string> textLanguages; //create dictionary for the text translation languages
        private Dictionary<string, List<TTsDetail>> voices; //convert a list into a dictionary and call it voices TTsDetails is a class in this file

        private WaveIn recorder; //WaveIn is a class

        private WaveFileWriter audioSent; //WaveFileWriter is a class

        private int audioBytesSent = 0;

        private BinaryMessageDecoder audioReceived; //BianryMessageDecoder is a testutils class

        private string correlationId;

        private SpeechClient s2smtClient; //SpeechClient is a class

        private WaveOut player; //WaveOut is a class

        private BufferedWaveProvider playerTextToSpeechWaveProvider; //BufferedWaveProvider is a class

        private BufferedWaveProvider playerAudioInputWaveProvider;

        private int textToSpeechBytes = 0;

        // If (DateTime.Now < suspendInputAudioUntil) then ignore input audio to avoid echo.
        private DateTime suspendInputAudioUntil = DateTime.MinValue; // DateTime is a struct - DateTime.MinValue represents the smallest possible value of dateTime

        private CancellationTokenSource streamAudioFromFileInterrupt = null; //CancellationTokenSource is a class

        // When auto-saving, save the slice Logs.Items[autoSaveFrom:]
        private int autoSaveFrom = 0;

        private string baseUrl = "dev.microsofttranslator.com";

        private class TTsDetail
        {
            public string Code { get; set; }
            public string DisplayName { get; set; }
        }

        private MiniWindow miniwindow;

        public MainWindow()
        {
            InitializeComponent();
            Debug.Print("This is a debug message");

            miniwindow = new MiniWindow();
            this.Closing += MainWindow_Closing;

            int waveInDevices = WaveIn.DeviceCount; //how many recording devices are there on the device
            for (int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++) //loop through and find all of the devices
            {
                WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(waveInDevice);
                Mic.Items.Add(new ComboBoxItem() { Content = deviceInfo.ProductName, Tag = waveInDevice }); //add the devices to the combo box to show the user
            }
            // Special case: audio source is a file
            Mic.Items.Add(new ComboBoxItem() { Content = "Play audio from file", Tag = "File" });

            Mic.SelectedIndex = Properties.Settings.Default.MicIndex;

            int waveOutDevices = WaveOut.DeviceCount; //get the waveout device count
            for (int waveOutDevice = 0; waveOutDevice < waveOutDevices; waveOutDevice++) //get all the wavout audio devices on the device and put them in a combo box
            {
                WaveOutCapabilities deviceInfo = WaveOut.GetCapabilities(waveOutDevice);
                Speaker.Items.Add(new ComboBoxItem() { Content = deviceInfo.ProductName, Tag = waveOutDevice });
            }

            Speaker.SelectedIndex = Properties.Settings.Default.SpeakerIndex;
            
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "1" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "2" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "3" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "4" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "5" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "6" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "7" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "8" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "9" });

            MiniWindow_Lines.SelectedIndex = Properties.Settings.Default.MiniWindow_Lines;
            miniwindow.SetFontSize(Properties.Settings.Default.MiniWindow_Lines);

            
            UpdateLanguageSettings(); //call a function with no arguments
            ShowMiniWindow.IsChecked = Properties.Settings.Default.ShowMiniWindow;
            FeatureTTS.IsChecked = Properties.Settings.Default.TTS;
            CutInputAudioCheckBox.IsChecked = Properties.Settings.Default.CutInputDuringTTS;
            FeaturePartials.IsChecked = Properties.Settings.Default.PartialResults;

        }



        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (miniwindow != null) miniwindow.Close();
            Properties.Settings.Default.ShowMiniWindow = ShowMiniWindow.IsChecked.Value;
            Properties.Settings.Default.TTS = FeatureTTS.IsChecked.Value;
            Properties.Settings.Default.CutInputDuringTTS = CutInputAudioCheckBox.IsChecked.Value;
            Properties.Settings.Default.PartialResults = FeaturePartials.IsChecked.Value;
            Properties.Settings.Default.ToLanguageIndex = ToLanguage.SelectedIndex;
            Properties.Settings.Default.Save();
        }

        private void UpdateHostButton_Click(object sender, RoutedEventArgs e) //this just calls the function right below it.
        {
            UpdateLanguageSettings();
        }

        private void UpdateLanguageSettings() //this function gets the language list from service by calling updatelanguagesettingsasync that method calls the api
        {
            UpdateUiState(UiState.GettingLanguageList); //call to method defined in this file near the end
            UpdateLanguageSettingsAsync().ContinueWith((t) =>
                {
                    var state = UiState.ReadyToConnect;
                    if (t.IsFaulted || t.IsCanceled)
                    {
                        state = UiState.MissingLanguageList;
                        this.Log(t.Exception, "E: Failed to get language list: {0}", t.IsCanceled ? "Timeout" : "");
                    }
                    this.SafeInvoke(() => { UpdateUiState(state); });
                });
        }

        private static string GetRequestId(HttpResponseMessage response)
        {
            IEnumerable<string> keys = null;
            if (!response.Headers.TryGetValues("X-RequestId", out keys))
                return null;
            return keys.First();
        }

        private async Task UpdateLanguageSettingsAsync() //build the URI for the call to get the languages - 
        {
            
            Uri baseUri = new Uri("https://" + baseUrl);
            Uri fullUri = new Uri(baseUri, "/Languages?api-version=1.0&scope=text,speech,tts");

            using (HttpClient client = new HttpClient()) //'client' is the var - using statement ensures the dispose method is used even after an exception.
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, fullUri);

                // get language names for current UI culture:
                request.Headers.Add("Accept-Language", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

                // add a client-side trace Id. In case of issues, one can contact support and provide this:
                //string traceId = "S2SMtDemoClient" + Guid.NewGuid().ToString();
                //request.Headers.Add("X-ClientTraceId", traceId);
                //Debug.Print("TraceId: {0}", traceId);

                client.Timeout = TimeSpan.FromMilliseconds(2000);
                HttpResponseMessage response = await client.SendAsync(request); //make the async call to the web using the client var and passing the built up URI
                response.EnsureSuccessStatusCode(); //causes exception if the return is false

                Debug.Print("Request Id returned: {0}", GetRequestId(response));

                //create dictionaries to hold the language specific data
                spokenLanguages = new Dictionary<string, string>();
                textLanguages = new Dictionary<string, string>();
                voices = new Dictionary<string, List<TTsDetail>>();

                JObject jResponse = JObject.Parse(await response.Content.ReadAsStringAsync()); //get the json from the async call with the response var created above, parse it and put it in a var called jResponse - JObject is a newton class

                //Gather the set of TTS voices
                foreach (JProperty jTts in jResponse["tts"])
                {
                    JObject ttsDetails = (JObject)jTts.Value;

                    string code = jTts.Name;
                    string language = ttsDetails["language"].ToString();
                    string displayName = ttsDetails["displayName"].ToString();
                    string gender = ttsDetails["gender"].ToString();

                    if (!voices.ContainsKey(language)) //check dictionary for a specific key value
                    {
                        voices.Add(language, new List<TTsDetail>()); //add to the dictionary the locale key and a ttsDetail object
                    }

                    voices[language].Add(new TTsDetail() { Code = code, DisplayName = string.Format("{0} ({1})", displayName, gender) });
                }

                // Gather the set of speech translation languages
                foreach (JProperty jSpeech in jResponse["speech"])
                {
                    JObject languageDetails = (JObject)jSpeech.Value;
                    string code = jSpeech.Name;
                    string displayName = languageDetails["name"].ToString();
                    spokenLanguages.Add(code, displayName);
                }

                spokenLanguages = spokenLanguages.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
                FromLanguage.Items.Clear();
                foreach (var language in spokenLanguages)
                {
                    FromLanguage.Items.Add(new ComboBoxItem() { Content = language.Value, Tag = language.Key});
                }

                // Gather the set of text translation languages
                foreach (JProperty jText in jResponse["text"])
                {
                    JObject languageDetails = (JObject)jText.Value;
                    string code = jText.Name;
                    string displayName = languageDetails["name"].ToString();
                    textLanguages.Add(code, displayName);
                }

                textLanguages = textLanguages.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
                ToLanguage.Items.Clear();
                foreach (var language in textLanguages)
                {
                    ToLanguage.Items.Add(new ComboBoxItem() { Content = language.Value, Tag = language.Key });
                }


                ToLanguage.SelectedIndex = Properties.Settings.Default.ToLanguageIndex;
                FromLanguage.SelectedIndex = Properties.Settings.Default.FromLanguageIndex;
            }
        }

        private void ToLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var voiceCombo = this.Voice;
            this.UpdateVoiceComboBox(voiceCombo, ToLanguage.SelectedItem as ComboBoxItem);
            miniwindow.DisplayText.Language = System.Windows.Markup.XmlLanguage.GetLanguage(((ComboBoxItem)this.ToLanguage.SelectedItem).Tag.ToString());
        }

        private void FromLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Properties.Settings.Default.FromLanguageIndex = FromLanguage.SelectedIndex;
        }

        private void UpdateVoiceComboBox(System.Windows.Controls.ComboBox voiceComboBox, ComboBoxItem languageSelectedItem)
        {
            voiceComboBox.Items.Clear();
            if (languageSelectedItem != null)
            {
                if (voices.ContainsKey(languageSelectedItem.Tag.ToString())) 
                {
                    var selectedVoice = voices[languageSelectedItem.Tag.ToString()];
                    foreach (var voice in selectedVoice)
                    {
                        voiceComboBox.Items.Add(new ComboBoxItem() { Content = voice.DisplayName, Tag = voice.Code });
                    }
                    voiceComboBox.SelectedIndex = 0;
                    FeatureTTS.IsEnabled = true;
                    CutInputAudioCheckBox.IsEnabled = true;
                    voiceComboBox.IsEnabled = true;
                }
                else
                {
                    FeatureTTS.IsEnabled = false;
                    CutInputAudioCheckBox.IsEnabled = false;
                    voiceComboBox.IsEnabled = false;
                }
            }
        }
        private void Mic_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = Mic.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string tag = selectedItem.Tag as string;
                this.AudioFileInput.Visibility = (tag == "File") ? Visibility.Visible : Visibility.Collapsed;
                this.AudioFileInputButton.Visibility = this.AudioFileInput.Visibility;
                Properties.Settings.Default.MicIndex = Mic.SelectedIndex;
            }
        }

        private void AudioFileInputButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog(); //**this code opens the file UI for file selection
            dialog.InitialDirectory = Properties.Settings.Default.OutputDirectory;
            dialog.Filter = "wav files (*.wav)|*.wav|All files (*.*)|*.*";
            dialog.FilterIndex = 1;
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.AudioFileInput.Text = dialog.FileName;
            }
        }

        private void StartListening_Click(object sender, RoutedEventArgs e) //this either starts the connection by calling Connect() or Disconnects by calling Disconnect()
        {
            switch (this.currentState)
            {
                case UiState.ReadyToConnect:
                    Connect();
                    break;
                case UiState.Connected:
                    Disconnect();
                    break;
                default:
                    return;
            }
        }

        private async Task ConnectAsync(SpeechClientOptions options, bool suspendInputAudioDuringTTS)
        {
            // Authenticate
            string admClientId = Properties.Settings.Default.ClientID;
            string admClientSecret = Properties.Settings.Default.ClientSecret;
            string ADMScope = "http://api.microsofttranslator.com";
            string ADMTokenUri = "https://datamarket.accesscontrol.windows.net/v2/OAuth2-13";
            ADMToken ADMAuthenticator = new ADMToken(ADMTokenUri, ADMScope);
            options.AuthHeaderValue = await ADMAuthenticator.GetToken(admClientId, admClientSecret);
            if (string.IsNullOrEmpty(options.AuthHeaderValue))
            {
                throw new InvalidDataException("Failed to get an authentication token.");
            }

            // Create the client
            TextMessageDecoder textDecoder;
            
            if (options.GetType() == typeof(SpeechTranslateClientOptions))
            {
                s2smtClient = new SpeechClient((SpeechTranslateClientOptions)options, CancellationToken.None);
                textDecoder = TextMessageDecoder.CreateTranslateDecoder();
            }
            else
            {
                throw new InvalidOperationException("Type of SpeechClientOptions in not supported.");
            }

            if (ShowMiniWindow.IsChecked.Value) miniwindow.Show();

            s2smtClient.OnBinaryData += (c, a) => { AddSamplesToPlay(a, suspendInputAudioDuringTTS); };
            s2smtClient.OnEndOfBinaryData += (c, a) => { AddSamplesToPlay(a, suspendInputAudioDuringTTS); };
            s2smtClient.OnTextData += (c, a) => { textDecoder.AppendData(a); };
            s2smtClient.OnEndOfTextData += (c, a) =>
            {
                textDecoder.AppendData(a);
                textDecoder
                    .Decode()
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Log(t.Exception, "E: Failed to decode incoming text message.");
                        }
                        else
                        {
                            object msg = t.Result;
                            if (msg.GetType() == typeof(FinalResultMessage))
                            {
                                var final = msg as FinalResultMessage;
                                Log("Final recognition {0}: {1}", final.Id, final.Recognition);
                                Log("Final translation {0}: {1}", final.Id, final.Translation);
                                this.SafeInvoke(() => SetMessage(final.Recognition, final.Translation, MessageKind.Chat));
                            }
                            if (msg.GetType() == typeof(PartialResultMessage))
                            {
                                var partial = msg as PartialResultMessage;
                                Log("Partial recognition {0}: {1}", partial.Id, partial.Recognition);
                                Log("Partial translation {0}: {1}", partial.Id, partial.Translation);
                                this.SafeInvoke(() => SetMessage(partial.Recognition, partial.Translation, MessageKind.Chat));
                            }
                        }
                    });
            };
            s2smtClient.Failed += (c, ex) =>
            {
                this.Log(ex, "E: SpeechTranslation client reported an error.");
            };
            s2smtClient.Disconnected += (c, ea) =>
            {
                this.SafeInvoke(() =>
                {
                    // We only care to react to server disconnect when our state is Connected. 
                    if (this.currentState == UiState.Connected)
                    {
                        this.Log("E: Connection has been lost.");
                        this.Disconnect();
                    }
                });
            };
            await s2smtClient.Connect();
        }

        private bool IsMissingInput(object item, string name)
        {
            ComboBoxItem cboItem = item as ComboBoxItem;
            if (item == null)
            {
                SetMessage(String.Format("No {0} selected.", name), "", MessageKind.Error);
                UpdateUiState(UiState.ReadyToConnect);
                return true;
            }
            return false;
        }

        private void Connect()
        {
            if (this.currentState != UiState.ReadyToConnect) return;

            Stopwatch watch = Stopwatch.StartNew();
            UpdateUiState(UiState.Connecting);
            miniwindow.Show();
            //This section is putting default values in case there are missing values in the UI
            // Minimal validation
            if (this.IsMissingInput(this.FromLanguage.SelectedItem, "source language")) return;
            if (this.IsMissingInput(this.ToLanguage.SelectedItem, "target language")) return;
            if (this.IsMissingInput(this.Voice.SelectedItem, "voice")) return;
            if (this.IsMissingInput(this.Profanity.SelectedItem, "profanity filter")) return;
            if (this.IsMissingInput(this.Mic.SelectedItem, "microphone")) return;
            if (this.IsMissingInput(this.Speaker.SelectedItem, "speaker")) return;

            if (this.LogAutoSave.IsChecked.Value)
            {
                this.autoSaveFrom = this.Logs.Items.Count;
            }

            string tag = ((ComboBoxItem)Mic.SelectedItem).Tag as string;
            string audioFileInputPath = null;
            if (tag == "File")
            {
                audioFileInputPath = this.AudioFileInput.Text;
                if (!File.Exists(audioFileInputPath))
                {
                    SetMessage(String.Format("Invalid audio source: selected file does not exist."), "", MessageKind.Error);
                    UpdateUiState(UiState.ReadyToConnect);
                    return;
                }
            }
            bool shouldSuspendInputAudioDuringTTS = this.CutInputAudioCheckBox.IsChecked.HasValue ? this.CutInputAudioCheckBox.IsChecked.Value : false;

            this.correlationId = Guid.NewGuid().ToString("D").Split('-')[0].ToUpperInvariant();

            // Setup speech translation client options
            SpeechClientOptions options;
            options = new SpeechTranslateClientOptions()
            {
                TranslateFrom = ((ComboBoxItem)this.FromLanguage.SelectedItem).Tag.ToString(),
                TranslateTo = ((ComboBoxItem)this.ToLanguage.SelectedItem).Tag.ToString(),
                Voice = ((ComboBoxItem)this.Voice.SelectedItem).Tag.ToString(),
            };
            
            options.Hostname = baseUrl;
            options.AuthHeaderKey = "Authorization";
            options.AuthHeaderValue = ""; // set later in ConnectAsync.
            options.ClientAppId = new Guid("EA66703D-90A8-436B-9BD6-7A2707A2AD99");
            options.CorrelationId = this.correlationId;
            options.Features = GetFeatures().ToString().Replace(" ", "");
            options.Profanity = ((SpeechClient.ProfanityFilter)Enum.Parse(typeof(SpeechClient.ProfanityFilter), ((ComboBoxItem)this.Profanity.SelectedItem).Tag.ToString(), true)).ToString();

            // Setup player and recorder but don't start them yet.
            WaveFormat waveFormat = new WaveFormat(16000, 16, 1);

            // WaveProvider for incoming TTS
            // We use a rather large BVufferDuration because we need to be able to hold an entire utterance.
            // TTS audio is received in bursts (faster than real-time).
            textToSpeechBytes = 0;
            playerTextToSpeechWaveProvider = new BufferedWaveProvider(waveFormat);
            playerTextToSpeechWaveProvider.BufferDuration = TimeSpan.FromMinutes(5);

            ISampleProvider sampleProvider = null;
            if (audioFileInputPath != null)
            {
                // Setup mixing of audio from input file and from TTS
                playerAudioInputWaveProvider = new BufferedWaveProvider(waveFormat);
                var srce1 = new Pcm16BitToSampleProvider(playerTextToSpeechWaveProvider);
                var srce2 = new Pcm16BitToSampleProvider(playerAudioInputWaveProvider);
                var mixer = new MixingSampleProvider(srce1.WaveFormat);
                mixer.AddMixerInput(srce1);
                mixer.AddMixerInput(srce2);
                sampleProvider = mixer;
            }
            else
            {
                recorder = new WaveIn();
                recorder.DeviceNumber = (int)((ComboBoxItem)Mic.SelectedItem).Tag;
                recorder.WaveFormat = waveFormat;
                recorder.DataAvailable += OnRecorderDataAvailable;
                sampleProvider = playerTextToSpeechWaveProvider.ToSampleProvider();
            }

            player = new WaveOut();
            player.DeviceNumber = (int)((ComboBoxItem)Speaker.SelectedItem).Tag;
            player.Init(sampleProvider);

            this.audioBytesSent = 0;

            string logAudioFileName = null;
            if (LogSentAudio.IsChecked.Value || LogReceivedAudio.IsChecked.Value)
            {
                string logAudioPath = System.IO.Path.Combine(Properties.Settings.Default.OutputDirectory, this.correlationId);
                Directory.CreateDirectory(logAudioPath);

                if (LogSentAudio.IsChecked.Value)
                {
                    logAudioFileName = System.IO.Path.Combine(logAudioPath, string.Format("audiosent_{0}.wav", this.correlationId));
                }

                if (LogReceivedAudio.IsChecked.Value)
                {
                    string fmt = System.IO.Path.Combine(logAudioPath, string.Format("audiotts_{0}_{{0}}.wav", this.correlationId));
                    this.audioReceived = new BinaryMessageDecoder(fmt);
                }
            }


            ConnectAsync(options, shouldSuspendInputAudioDuringTTS).ContinueWith((t) =>
            {
                if (t.IsFaulted || t.IsCanceled || !s2smtClient.IsConnected()) //t.isfaulted OR t.iscancelled OR NOT s2smtclient.isconnected() do the following
                {
                    this.Log(t.Exception, "E: Unable to connect: cid='{0}', elapsedMs='{1}'.",
                        this.correlationId, watch.ElapsedMilliseconds);
                    this.SafeInvoke(() => {
                        this.AutoSaveLogs();
                        this.UpdateUiState(UiState.ReadyToConnect);
                    });
                }
                else
                {
                    // Start playing incoming audio
                    player.Play();
                    // Start recording and sending
                    if (logAudioFileName != null)
                    {
                        audioSent = new WaveFileWriter(logAudioFileName, waveFormat);
                        this.Log("I: Recording outgoing audio in {0}", logAudioFileName);
                    }
                    // Send the WAVE header
                    s2smtClient.SendBinaryMessage(new ArraySegment<byte>(GetWaveHeader(waveFormat)));
                    if (audioFileInputPath != null)
                    {
                        streamAudioFromFileInterrupt = new CancellationTokenSource();
                        Task.Run(() => this.StreamFile(audioFileInputPath, streamAudioFromFileInterrupt.Token))
                            .ContinueWith((x) =>
                            {
                                if (x.IsFaulted)
                                {
                                    this.Log(x.Exception, "E: Error while playing audio from input file.");
                                }
                                else
                                {
                                    this.Log("I: Done playing audio from input file.");
                                }
                            });
                    }
                    else
                    {
                        // Start sending audio from the recoder.
                        recorder.StartRecording();
                    }
                    this.Log("I: Connected: cid='{0}', elapsedMs='{1}'.",
                        this.correlationId, watch.ElapsedMilliseconds);
                    this.SafeInvoke(() => this.UpdateUiState(UiState.Connected));
                }
            }).ContinueWith((t) => {
                if (t.IsFaulted)
                {
                    Log(t.Exception, "E: Failed to start sending audio.");
                    this.SafeInvoke(() => { 
                        this.AutoSaveLogs();
                        this.UpdateUiState(UiState.ReadyToConnect);
                    });
                }
            });
        }

        private void StreamFile(string path, CancellationToken token)
        {
            var audioSource = new AudioSourceCollection(new IAudioSource[] {
                new WavFileAudioSource(path, true),
                new WavSilenceAudioSource(2000),
            });

            int audioChunkSizeInMs = 100;
            var handle = new AutoResetEvent(true);
            long audioChunkSizeInTicks = TimeSpan.TicksPerMillisecond * (long)(audioChunkSizeInMs);
            long tnext = DateTime.Now.Ticks + audioChunkSizeInMs;
            int wait = audioChunkSizeInMs;
            foreach (var chunk in audioSource.Emit(audioChunkSizeInMs))
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                // Send chunk to speech translation service
                this.OnAudioDataAvailable(chunk);
                // Send chunk to local audio player via the mixer
                playerAudioInputWaveProvider.AddSamples(chunk.Array, chunk.Offset, chunk.Count);

                handle.WaitOne(wait);
                tnext = tnext + audioChunkSizeInTicks;
                wait = (int)((tnext - DateTime.Now.Ticks) / TimeSpan.TicksPerMillisecond);
                if (wait < 0) wait = 0;
            }
        }

        private void Disconnect()
        {
            if (this.currentState != UiState.Connected) return;

            UpdateUiState(UiState.Disconnecting);

            miniwindow.Hide();

            if (recorder != null)
            {
                recorder.StopRecording();
                recorder.DataAvailable -= OnRecorderDataAvailable;
                recorder.Dispose();
                recorder = null;
            }

            if (streamAudioFromFileInterrupt != null)
            {
                streamAudioFromFileInterrupt.Cancel();
                streamAudioFromFileInterrupt = null;
            }

            if (player != null)
            {
                player.Stop();
                player.Dispose();
                player = null;
            }

            // Close the audio file if logging
            if (audioSent != null)
            {
                audioSent.Flush();
                audioSent.Dispose();
                audioSent = null;
            }

            if (this.audioReceived != null)
            {
                this.audioReceived.Dispose();
                this.audioReceived = null;
            }

            var task = s2smtClient.Disconnect()
                .ContinueWith((t) =>
                {
                    if (t.IsFaulted)
                    {
                        this.Log(t.Exception, "E: Disconnect call to client failed.");
                    }
                    s2smtClient.Dispose();
                    s2smtClient = null;
                })
                .ContinueWith((t) => {
                    if (t.IsFaulted)
                    {
                        this.Log(t.Exception, "E: Disconnected but there were errors.");
                    }
                    else
                    {
                        this.Log("I: Disconnected.");
                    }
                    this.SafeInvoke(() => {
                        this.AutoSaveLogs();
                        this.UpdateUiState(UiState.ReadyToConnect);
                    });
                });
        }

        private void AddSamplesToPlay(ArraySegment<byte> a, bool suspendInputAudioDuringTTS)
        {
            int offset = a.Offset;
            int count = a.Count;
            if (this.textToSpeechBytes <= 0)
            {
                int chunkType = BitConverter.ToInt32(a.Array, a.Offset);
                if (chunkType != 0x46464952) throw new InvalidDataException("Invalid WAV file");
                int size = (int)(BitConverter.ToUInt32(a.Array, a.Offset + 4));
                int riffType = BitConverter.ToInt32(a.Array, a.Offset + 8);
                if (riffType != 0x45564157) throw new InvalidDataException("Invalid WAV file");
                textToSpeechBytes = size;
                if (suspendInputAudioDuringTTS)
                {
                    // Assumes PCM: (TTS audio duration in ms) = (size in bytes) / (32 bytes / ms)
                    this.suspendInputAudioUntil = DateTime.Now.AddMilliseconds(size / 32);
                    this.UpdateUiForInputAudioOnOff(false);
                }
                offset += 44;
                count -= 44;
            }
            playerTextToSpeechWaveProvider.AddSamples(a.Array, offset, count);
            textToSpeechBytes -= a.Count;

            if (this.audioReceived != null)
            {
                this.audioReceived.AppendData(a);
            }
        }

        private void TraceButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            Logs.Items.Clear();
            this.autoSaveFrom = 0;
        }

        private void AutoSaveLogs()
        {
            if (this.LogAutoSave.IsChecked.Value == false)
            {
                return;
            }

            string cid = String.IsNullOrEmpty(this.correlationId) ? "no-cid" : this.correlationId;
            string filename = System.IO.Path.Combine(Properties.Settings.Default.OutputDirectory, string.Format("log-{0}.txt", cid));
            using (var writer = new StreamWriter(filename))
            {
                for (int i = this.autoSaveFrom; i < Logs.Items.Count; i++)
                {
                    writer.WriteLine(Logs.Items[i].ToString());
                }
            }
            this.autoSaveFrom = this.Logs.Items.Count;
        }

        private void SaveLogs_Click(object sender, RoutedEventArgs e)
        {
            if(!Directory.Exists(Properties.Settings.Default.OutputDirectory))
                Directory.CreateDirectory(Properties.Settings.Default.OutputDirectory);
            var dlg = new System.Windows.Forms.SaveFileDialog();
            dlg.InitialDirectory = Properties.Settings.Default.OutputDirectory;
            dlg.FileName = string.Format("log-{0}.txt", String.IsNullOrEmpty(this.correlationId) ? "no-cid" : this.correlationId);
            dlg.DefaultExt = "txt";
            dlg.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            dlg.FilterIndex = 1;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                using (var writer = new StreamWriter(dlg.OpenFile()))
                {
                    foreach (var item in Logs.Items)
                    {
                        writer.WriteLine(item.ToString());
                    }
                }
            }
        }

        private string Now() { return DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.ff", DateTimeFormatInfo.InvariantInfo); }

        private void Log(string text)
        {
            this.AddItemToLog(string.Format("{0} {1}", Now(), text));
        }

        private void Log(string format, params object[] args)
        {
            this.AddItemToLog(string.Format("{0} {1}", Now(), string.Format(format, args)));
        }

        private void Log(Exception exception, string format, params object[] args)
        {
            var s = new StringBuilder();
            s.Append(string.Format("{0} {1}", Now(), string.Format(format, args)));
            PrintException(exception, s);
            this.AddItemToLog(s.ToString());
        }

        private void AddItemToLog(string text)
        {
            Dispatcher.Invoke(() => {
                int pos = this.Logs.Items.Add(text);
                this.Logs.ScrollIntoView(this.Logs.Items.GetItemAt(pos));
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }

        private void PrintException(Exception exception, StringBuilder s)
        {
            if (exception == null) return;
            if (!(exception is AggregateException))
            {
                s.AppendLine(" ").Append(exception.Message);
            }
            PrintException(exception.InnerException, s);
        }

        private void OnRecorderDataAvailable(object sender, WaveInEventArgs e)
        {
            this.OnAudioDataAvailable(new ArraySegment<byte>(e.Buffer, 0, e.BytesRecorded));
        }

        private void OnAudioDataAvailable(ArraySegment<byte> data)
        {
            if (DateTime.Now < this.suspendInputAudioUntil)
            {
                Array.Clear(data.Array, data.Offset, data.Count);
            }
            else
            {
                this.suspendInputAudioUntil = DateTime.MinValue;
                this.UpdateUiForInputAudioOnOff(true);
            }
            if (audioSent != null)
            {
                audioSent.Write(data.Array, data.Offset, data.Count);
            }
            if (s2smtClient != null)
            {
                s2smtClient.SendBinaryMessage(new ArraySegment<byte>(data.Array, data.Offset, data.Count));
                audioBytesSent += data.Count;
                this.SafeInvoke(() => this.AudioBytesSentLabel.Content = audioBytesSent);
            }
        }

        private SpeechClient.Features GetFeatures()
        {
            SpeechClient.Features features = 0;

            if (FeaturePartials.IsChecked.Value)
                features |= SpeechClient.Features.Partial;
            if (FeatureTTS.IsChecked.Value)
                features |= SpeechClient.Features.TextToSpeech;
            
            return features;
        }

        private byte[] GetWaveHeader(WaveFormat format)
        {
            using(MemoryStream stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8);
                writer.Write(Encoding.UTF8.GetBytes("RIFF"));
                writer.Write(0);
                writer.Write(Encoding.UTF8.GetBytes("WAVE"));
                writer.Write(Encoding.UTF8.GetBytes("fmt "));
                format.Serialize(writer);
                writer.Write(Encoding.UTF8.GetBytes("data"));
                writer.Write(0);

                stream.Position = 0;
                byte[] buffer = new byte[stream.Length];
                stream.Read(buffer, 0, buffer.Length);
                return buffer;
            }         
        }

        private void SetMessage(string top, string bottom, MessageKind kind)
        {
            Brush borderBrush = Brushes.LightGray;
            Visibility borderVisibiliy = Visibility.Visible;
            Brush foreground1 = Brushes.Black;
            Brush foreground2 = Brushes.Green;
            string top1 = top;
            string top2 = "";
            string bottom1 = bottom;
            string bottom2 = "";

            switch (kind)
            {
                case MessageKind.Error:
                    borderBrush = Brushes.Red;
                    foreground1 = Brushes.Red;
                    borderVisibiliy = Visibility.Collapsed;
                    break;
                case MessageKind.Status:
                    borderBrush = Brushes.Green;
                    foreground1 = Brushes.Green;
                    borderVisibiliy = Visibility.Collapsed;
                    break;
                case MessageKind.ChatDetect1:
                    top1 = top;
                    top2 = bottom;
                    bottom1 = bottom2 = null;
                    break;
                case MessageKind.ChatDetect2:
                    top1 = top2 = null;
                    bottom1 = top;
                    bottom2 = bottom;
                    break;
                case MessageKind.Chat:
                default:
                    break;
            }

            this.DialogRecognitionBorder.BorderBrush = borderBrush;
            this.DialogTranslationBorder.BorderBrush = borderBrush;
            this.DialogBorder.BorderBrush = borderBrush;
            this.DialogBorder.Visibility = borderVisibiliy;

            this.TopRun1.Foreground = foreground1;
            this.TopRun2.Foreground = foreground2;
            this.BottomRun1.Foreground = foreground1;
            this.BottomRun2.Foreground = foreground2;
            if (top1 != null) this.TopRun1.Text = top1;
            if (top2 != null) this.TopRun2.Text = top2;
            if (bottom1 != null)
            {
                this.BottomRun1.Text = bottom1;
                if (kind == MessageKind.Chat) miniwindow.DisplayText.Text = bottom1;
            }
            if (bottom2 != null) this.BottomRun2.Text = bottom2;
        }


        //this method shows the audio input if an audio file is selected
        private void UpdateUiForInputAudioOnOff(bool isOn)
        {
            this.SafeInvoke(() => this.CutInputAudioLabel.Visibility = isOn ? Visibility.Collapsed : Visibility.Visible);            
        }

        private void UpdateUiState(UiState state)
        {
            this.currentState = state;
            bool isInputAllowed = true;
            this.AudioBytesSentLabel.Visibility = Visibility.Collapsed;

            switch (state)
            {
                case UiState.GettingLanguageList:
                    this.StartListening.IsEnabled = false;
                    this.SetMessage(string.Format("Getting language list from {0}", this.baseUrl), "", MessageKind.Status);
                    isInputAllowed = false;
                    break;
                case UiState.MissingLanguageList:
                    this.StartListening.IsEnabled = false;
                    this.SetMessage(string.Format("Failed to get language list. Please re-enter your account settings."), "", MessageKind.Error);
                    isInputAllowed = true;
                    break;
                case UiState.ReadyToConnect:
                    this.StartListening.IsEnabled = true;
                    this.SetMessage(string.Format("Set your options, then click \"Start\" to start."), "", MessageKind.Status);
                    this.StartListening.Content = "Start";
                    break;
                case UiState.Connecting:
                    this.StartListening.IsEnabled = false;
                    this.SetMessage(string.Format("Connecting..."), "", MessageKind.Status);
                    isInputAllowed = false;
                    break;
                case UiState.Connected:
                    this.StartListening.IsEnabled = true;
                    this.AudioBytesSentLabel.Visibility = Visibility.Visible;
                    this.SetMessage("Connected! After you start speaking, transcripts in the source language will show here...",
                            "...and translations to the target language will show here.", MessageKind.Status);
                    this.StartListening.Content = "Stop";
                    //this.TraceCmd.Text = this.GetTraceCmd();
                    this.Log("I: {0}", this.TraceCmd.Text);
                    isInputAllowed = false;
                    break;
                case UiState.Disconnecting:
                    this.StartListening.IsEnabled = false;
                    this.SetMessage(string.Format("Disconnecting..."), "", MessageKind.Status);
                    isInputAllowed = false;
                    break;
                default:
                    break;
            }


            this.Mic.IsEnabled = isInputAllowed;
            this.AudioFileInput.IsEnabled = isInputAllowed;
            this.AudioFileInputButton.IsEnabled = isInputAllowed;
            this.Speaker.IsEnabled = isInputAllowed;
            this.FromLanguage.IsEnabled = isInputAllowed;
            this.ToLanguage.IsEnabled = isInputAllowed;
            this.Voice.IsEnabled = isInputAllowed;
            this.FeaturePartials.IsEnabled = isInputAllowed;
            this.FeatureTTS.IsEnabled = isInputAllowed;
            this.CutInputAudioCheckBox.IsEnabled = isInputAllowed;
            this.UpdateSettings.Visibility = Visibility.Collapsed;
            this.Profanity.IsEnabled = isInputAllowed;
            this.ShowMiniWindow.IsEnabled = isInputAllowed;

            this.SaveLogs.IsEnabled = isInputAllowed;
            this.ClearLogs.IsEnabled = true;
            this.LogAutoSave.IsEnabled = isInputAllowed;
            this.LogSentAudio.IsEnabled = isInputAllowed;
            this.LogReceivedAudio.IsEnabled = isInputAllowed;
        }

        private void SafeInvoke(Action action)
        {
            if (this.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Dispatcher.Invoke(action);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow sw = new SettingsWindow();
            sw.Show();
            sw.Closing += SettingsWindowClosing;
        }

        private void SettingsWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UpdateLanguageSettings();
        }

        private void Speaker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Properties.Settings.Default.SpeakerIndex = Speaker.SelectedIndex;
        }

        private void MiniWindow_Lines_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            miniwindow.SetFontSize(MiniWindow_Lines.SelectedIndex);
            Properties.Settings.Default.MiniWindow_Lines = MiniWindow_Lines.SelectedIndex;
        }

    }

    [DataContract]
    public class AppUserSettings
    {
        [DataMember(EmitDefaultValue = false)]
        public string OutputDirectory { get; set; }
    }

}
