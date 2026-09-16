using System;
using System.Collections;
using OpenNPC.Dialogue.Mock;
using UnityEngine;

namespace OpenNPC.Dialogue.Providers
{
    /// <summary>
    /// Offline provider. Reads the persona and the player's message and composes a
    /// reply from the persona's own knowledge, opinions and voice. No network, no
    /// model, no API key — so the demo works anywhere, including a static WebGL page.
    ///
    /// This is NOT an AI. It exists to prove the architecture: swap it for
    /// OpenNPCDialogueProvider (or your own IDialogueProvider) and nothing else changes.
    /// </summary>
    public sealed class MockDialogueProvider : IDialogueProvider
    {
        private readonly MonoBehaviour _host;
        private readonly Vector2 _latency;
        private readonly MockResponseComposer _composer = new MockResponseComposer();

        public string Name => "MockDialogueProvider";

        /// <param name="host">Runs the simulated latency coroutine. Null = respond synchronously.</param>
        /// <param name="latency">Simulated thinking time range in seconds.</param>
        public MockDialogueProvider(MonoBehaviour host = null, Vector2 latency = default)
        {
            _host = host;
            _latency = latency;
        }

        /// <summary>Synchronous entry point, used by tests and tools.</summary>
        public DialogueResponse Respond(DialogueRequest request) => _composer.Compose(request);

        public void Generate(DialogueRequest request, Action<DialogueResponse> onComplete)
        {
            DialogueResponse response;
            try
            {
                response = _composer.Compose(request);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                response = DialogueResponse.Error("(mock provider error)");
            }

            if (_host == null || !_host.isActiveAndEnabled || _latency.y <= 0f)
            {
                onComplete(response);
                return;
            }
            _host.StartCoroutine(Deliver(response, onComplete));
        }

        private IEnumerator Deliver(DialogueResponse response, Action<DialogueResponse> onComplete)
        {
            // Longer answers "take longer to think of" — purely cosmetic.
            float t = Mathf.Lerp(_latency.x, _latency.y, Mathf.Clamp01(response.Text.Length / 140f));
            yield return new WaitForSeconds(t);
            onComplete(response);
        }
    }
}
