using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenNPC.Personas;
using UnityEngine;
using UnityEngine.Networking;

namespace OpenNPC.Dialogue.Providers
{
    /// <summary>
    /// PLACEHOLDER integration with an OpenNPC backend.
    ///
    /// What it does: POSTs the documented request JSON
    /// (framework/schemas/dialogue-request.schema.json) to <c>endpoint</c> and parses
    /// the documented response JSON. What it does NOT do: there is no OpenNPC
    /// backend in this repository yet. Point it at your own server (the stub in
    /// framework/examples/backend-stub implements the contract) or it will return
    /// an error line — it never fakes an answer.
    ///
    /// Security: this runs inside the player's browser/game. Never add model API
    /// keys here. The backend holds credentials and calls the model.
    /// </summary>
    public sealed class OpenNPCDialogueProvider : IDialogueProvider
    {
        private readonly MonoBehaviour _host;
        private readonly string _endpoint;
        private readonly float _timeout;

        public string Name => "OpenNPCDialogueProvider";
        public string Endpoint => _endpoint;

        public OpenNPCDialogueProvider(MonoBehaviour host, string endpoint, float timeoutSeconds)
        {
            _host = host ? host : throw new ArgumentNullException(nameof(host));
            _endpoint = endpoint;
            _timeout = timeoutSeconds;
        }

        public void Generate(DialogueRequest request, Action<DialogueResponse> onComplete)
        {
            if (string.IsNullOrWhiteSpace(_endpoint))
            {
                onComplete(DialogueResponse.Error("(OpenNPC provider has no endpoint configured)"));
                return;
            }
            _host.StartCoroutine(Send(request, onComplete));
        }

        public static string ToJson(DialogueRequest request) => JsonUtility.ToJson(WireRequest.From(request));

        public static DialogueResponse FromJson(string json)
        {
            WireResponse wire = JsonUtility.FromJson<WireResponse>(json);
            if (wire == null || string.IsNullOrEmpty(wire.text))
                return DialogueResponse.Error("(OpenNPC backend returned no text)");
            return new DialogueResponse(wire.text, wire.emotion, string.IsNullOrEmpty(wire.mood) ? null : wire.mood,
                                        wire.end_conversation);
        }

        private IEnumerator Send(DialogueRequest request, Action<DialogueResponse> onComplete)
        {
            byte[] body = Encoding.UTF8.GetBytes(ToJson(request));
            using (var www = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                www.uploadHandler = new UploadHandlerRaw(body);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Accept", "application/json");
                www.timeout = Mathf.CeilToInt(_timeout);
                yield return www.SendWebRequest();

                DialogueResponse response;
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[OpenNPC] Backend request failed: {www.responseCode} {www.error} ({_endpoint})");
                    response = DialogueResponse.Error("(can't reach the OpenNPC backend)");
                }
                else
                {
                    try
                    {
                        response = FromJson(www.downloadHandler.text);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[OpenNPC] Could not parse backend response: " + e.Message);
                        response = DialogueResponse.Error("(OpenNPC backend sent invalid JSON)");
                    }
                }
                onComplete(response);
            }
        }

        // Wire format: snake_case, exactly as the JSON schemas. Kept private so the
        // rest of the engine only ever sees DialogueRequest / DialogueResponse.
        [Serializable]
        private sealed class WireRequest
        {
            public Persona persona;
            public WireContext context;
            public List<WireTurn> conversation;
            public string player_message;

            public static WireRequest From(DialogueRequest r)
            {
                var w = new WireRequest
                {
                    persona = r.Persona,
                    player_message = r.PlayerMessage,
                    conversation = new List<WireTurn>(),
                    context = new WireContext
                    {
                        location = r.Context.location,
                        time_of_day = r.Context.timeOfDay,
                        npc_state = r.Context.npcState,
                        npc_mood = r.Context.npcMood,
                        nearby = r.Context.nearby,
                        session_id = r.Context.sessionId,
                        @event = r.Context.dialogueEvent,
                    },
                };
                foreach (ConversationTurn t in r.Conversation)
                    w.conversation.Add(new WireTurn { role = t.role == ConversationRole.Player ? "player" : "npc", content = t.content });
                return w;
            }
        }

        [Serializable]
        private sealed class WireContext
        {
            public string location;
            public string time_of_day;
            public string npc_state;
            public string npc_mood;
            public List<string> nearby;
            public string session_id;
            public string @event;
        }

        [Serializable]
        private sealed class WireTurn
        {
            public string role;
            public string content;
        }

        [Serializable]
        private sealed class WireResponse
        {
            public string text;
            public string emotion;
            public string mood;
            public bool end_conversation;
        }
    }
}
