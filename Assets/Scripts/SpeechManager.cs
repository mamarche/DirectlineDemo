using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;
using Microsoft.CognitiveServices.Speech;
using System;

public delegate void AudioClipHandler();

public class SpeechManager : Singleton<SpeechManager>
{
    [SerializeField] private string SpeechServicesSubscriptionKey = "Your Subscription Key";
    [SerializeField] private string SpeechServicesRegion = "westeurope";
    [SerializeField] private string fromLanguage = "en-us";
    [SerializeField] private AudioSource audioSource;

    private bool isRecognized = false;
    private string errorString;
    private string recognizedString;
    private SpeechConfig speechConfig;
    private SpeechRecognizer recognizer;
    private System.Object threadLocker = new System.Object();

    public event AudioClipHandler OnAudioEnded;

    private void Start()
    {
        speechConfig = SpeechConfig.FromSubscription(SpeechServicesSubscriptionKey, SpeechServicesRegion);
    }

    private void Update()
    {
        if (isRecognized)
        {
            lock (threadLocker)
            {
                DirectlineManager.Instance.SetMessage(recognizedString);
                isRecognized = false;
            }
        }
    }

    private void OnDisable()
    {
        StopRecognition();
    }

    public void Speech(string text)
    {
        using (var synthsizer = new SpeechSynthesizer(speechConfig, null))
        {
            // Starts speech synthesis, and returns after a single utterance is synthesized.
            var result = synthsizer.SpeakTextAsync(text).Result;

            // Checks result.
            string newMessage = string.Empty;
            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                var audioClip = ByteArrayToClip(result.AudioData);
                audioSource.clip = audioClip;

                Invoke("AudioEnd", audioClip.length);

                audioSource.Play();

                Debug.Log("Speech synthesis succeeded!");
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                Debug.Log($"CANCELED:\nReason=[{cancellation.Reason}]\nErrorDetails=[{cancellation.ErrorDetails}]\nDid you update the subscription info?");
            }
        }
    }

    private void AudioEnd()
    {
        if (OnAudioEnded != null)
            OnAudioEnded();
    }

    public async void StartContinuousRecognition()
    {
        UnityEngine.Debug.LogFormat("Starting Continuous Speech Recognition.");
        CreateSpeechRecognizer();

        if (recognizer != null)
        {
            UnityEngine.Debug.LogFormat("Starting Speech Recognizer.");
            await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

            
            UnityEngine.Debug.LogFormat("Speech Recognizer is now running.");
        }
        UnityEngine.Debug.LogFormat("Start Continuous Speech Recognition exit");
    }

    public async void StopRecognition()
    {
        if (recognizer != null)
        {
            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            recognizer.Recognizing -= RecognizingHandler;
            recognizer.Recognized -= RecognizedHandler;
            recognizer.SpeechStartDetected -= SpeechStartDetectedHandler;
            recognizer.SpeechEndDetected -= SpeechEndDetectedHandler;
            recognizer.Canceled -= CanceledHandler;
            recognizer.SessionStarted -= SessionStartedHandler;
            recognizer.SessionStopped -= SessionStoppedHandler;
            recognizer.Dispose();
            recognizer = null;
            UnityEngine.Debug.LogFormat("Speech Recognizer is now stopped.");
        }
    }

    #region Speech Recognition event handlers
    private void SessionStartedHandler(object sender, SessionEventArgs e)
    {
        isRecognized = false;
        UnityEngine.Debug.LogFormat($"\n    Session started event. Event: {e.ToString()}.");
    }

    private void SessionStoppedHandler(object sender, SessionEventArgs e)
    {
        UnityEngine.Debug.LogFormat($"\n    Session event. Event: {e.ToString()}.");
        UnityEngine.Debug.LogFormat($"Session Stop detected. Stop the recognition.");
    }

    private void SpeechStartDetectedHandler(object sender, RecognitionEventArgs e)
    {
        isRecognized = false;
        UnityEngine.Debug.LogFormat($"SpeechStartDetected received: offset: {e.Offset}.");
    }

    private void SpeechEndDetectedHandler(object sender, RecognitionEventArgs e)
    {
        UnityEngine.Debug.LogFormat($"SpeechEndDetected received: offset: {e.Offset}.");
        UnityEngine.Debug.LogFormat($"Speech end detected.");
    }

    // "Recognizing" events are fired every time we receive interim results during recognition (i.e. hypotheses)
    private void RecognizingHandler(object sender, SpeechRecognitionEventArgs e)
    {
        isRecognized = false;
        
    }

    // "Recognized" events are fired when the utterance end was detected by the server
    private void RecognizedHandler(object sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizedSpeech)
        {
            UnityEngine.Debug.LogFormat($"RECOGNIZED: Text={e.Result.Text}");
            lock (threadLocker)
            {
                recognizedString = $"{e.Result.Text}";
            }
            isRecognized = !string.IsNullOrEmpty(recognizedString);
        }
        else if (e.Result.Reason == ResultReason.NoMatch)
        {
            UnityEngine.Debug.LogFormat($"NOMATCH: Speech could not be recognized.");
        }
    }

    // "Canceled" events are fired if the server encounters some kind of error.
    // This is often caused by invalid subscription credentials.
    private void CanceledHandler(object sender, SpeechRecognitionCanceledEventArgs e)
    {
        isRecognized = false;
        UnityEngine.Debug.LogFormat($"CANCELED: Reason={e.Reason}");

        errorString = e.ToString();
        if (e.Reason == CancellationReason.Error)
        {
            UnityEngine.Debug.LogFormat($"CANCELED: ErrorDetails={e.ErrorDetails}");
            UnityEngine.Debug.LogFormat($"CANCELED: Did you update the subscription info?");
        }
    }
    #endregion

    #region Helpers
    private void CreateSpeechRecognizer()
    {
        if (SpeechServicesSubscriptionKey.Length == 0 || SpeechServicesSubscriptionKey == "YourSubscriptionKey")
        {
            errorString = "ERROR: Missing service credentials";
            UnityEngine.Debug.LogFormat(errorString);
            return;
        }
        UnityEngine.Debug.LogFormat("Creating Speech Recognizer.");

        if (recognizer == null)
        {
            SpeechConfig config = SpeechConfig.FromSubscription(SpeechServicesSubscriptionKey, SpeechServicesRegion);
            config.SpeechRecognitionLanguage = fromLanguage;
            recognizer = new SpeechRecognizer(config);

            if (recognizer != null)
            {
                // Subscribes to speech events.
                recognizer.Recognizing += RecognizingHandler;
                recognizer.Recognized += RecognizedHandler;
                recognizer.SpeechStartDetected += SpeechStartDetectedHandler;
                recognizer.SpeechEndDetected += SpeechEndDetectedHandler;
                recognizer.Canceled += CanceledHandler;
                recognizer.SessionStarted += SessionStartedHandler;
                recognizer.SessionStopped += SessionStoppedHandler;
            }
        }
        UnityEngine.Debug.LogFormat("CreateSpeechRecognizer exit");
    }
    private AudioClip ByteArrayToClip(byte[] data)
    {
        // Since native playback is not yet supported on Unity yet (currently only supported on Windows/Linux Desktop),
        // use the Unity API to play audio here as a short term solution.
        // Native playback support will be added in the future release.
        var sampleCount = data.Length / 2;
        var audioData = new float[sampleCount];
        for (var i = 0; i < sampleCount; ++i)
        {
            audioData[i] = (short)(data[i * 2 + 1] << 8 | data[i * 2]) / 32768.0F;
        }

        // The default output audio format is 16K 16bit mono
        var audioClip = AudioClip.Create("SynthesizedAudio", sampleCount, 1, 16000, false);
        audioClip.SetData(audioData, 0);
        return audioClip;
    }
    #endregion

}
