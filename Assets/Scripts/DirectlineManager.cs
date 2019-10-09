using Assets.BotDirectLine;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.Rendering;
using UnityEngine;

public class DirectlineManager : Singleton<DirectlineManager>
{
    [SerializeField]
    private string directlineSecret;
    [SerializeField]
    private TMP_InputField inputField;
    [SerializeField]
    private TMP_Text responseText;
    [SerializeField]
    private CharacterController character;

    private ConversationState _conversationState = new ConversationState();
    
    void Start()
    {
        BotDirectLineManager.Initialize(directlineSecret);
        
        StartCoroutine(BotDirectLineManager.Instance.StartConversationCoroutine());
    }

    internal void SendDirectlineMessage(string text)
    {
        StartCoroutine(BotDirectLineManager.Instance.SendMessageCoroutine(_conversationState.ConversationId,
            "MyUserId", text, "My User"));
    }
    public void OnSendMessage()
    {
        SpeechManager.Instance.StopRecognition();
       SendDirectlineMessage(inputField.text);
    }

    internal void SetMessage(string recognizedString)
    {
        inputField.text = recognizedString;
        OnSendMessage();
    }

    public void OnStartRecognitionCommand()
    {
        SpeechManager.Instance.StartContinuousRecognition();
    }

    private void OnEnable()
    {
        BotDirectLineManager.Instance.BotResponse += Instance_BotResponse;
        SpeechManager.Instance.OnAudioEnded += () => { character.StopTalking(); };
    }
    private void OnDisable()
    {
        BotDirectLineManager.Instance.BotResponse -= Instance_BotResponse;
    }

    private void Instance_BotResponse(object sender, Assets.BotDirectLine.BotResponseEventArgs e)
    {
        switch (e.EventType)
        {
            case Assets.BotDirectLine.EventTypes.ConversationStarted:
                _conversationState.ConversationId = e.ConversationId;
                if (!string.IsNullOrEmpty(_conversationState.ConversationId))
                {
                    StartCoroutine(BotDirectLineManager.Instance.GetMessagesCoroutine(_conversationState.ConversationId));
                }
                Debug.Log("Conversation Started");
                break;
            case Assets.BotDirectLine.EventTypes.MessageReceived:

                int messageId = Convert.ToInt32(e.Watermark);

                responseText.text = e.Messages[messageId].Text;

                SpeechManager.Instance.Speech(responseText.text);
                character.StartTalking();
                Debug.Log($"Message Received: {e.Messages[messageId].Text}");
                
                break;
            case Assets.BotDirectLine.EventTypes.MessageSent:
                if (!string.IsNullOrEmpty(_conversationState.ConversationId))
                {
                    StartCoroutine(BotDirectLineManager.Instance.GetMessagesCoroutine(_conversationState.ConversationId));
                }
                Debug.Log("Message Sent");
                break;
            case Assets.BotDirectLine.EventTypes.Error:
                Debug.Log($"Error: {e.Message}");
                break;
            default:
                break;
        }
    }
}
