using AMP;
using AMP.DedicatedServer;
using AMP.Logging;
using AMP.Network.Data;
using AMP.Network.Packets.Implementation;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace HideAndSeekPlugin {
    public class HideAndSeek : AMP_Plugin {
        public override string NAME    { get { return "HideAndSeek"; } }
        public override string VERSION { get { return "0.0.2"; } }
        public override string AUTHOR  { get { return "flexhd""; } }

        private bool gameRunning = false;
        private List<ClientData> hiders = new List<ClientData>();
        private ClientData seeker = null;
        private float hideTime = 30.0f; // Hide time in seconds
        private IEnumerator HideTimeCountdown() {
            yield return new WaitForSeconds(hideTime);

            // Notify players about the game starting.
            ModManager.serverInstance.SendReliableToAll(new DisplayTextPacket("game_start", "Hide time is over Hide and Seek game started!", Color.yellow, Vector3.forward * 2, true, true, 10));
        }



        public override void OnStart() {
            Log.Info(NAME, "Hide and Seek plugin started.");
        }

        public override void OnPlayerJoin(ClientInformation client) {
            if (!gameRunning) {
                hiders.Add(client);
                Log.Info(NAME, $"{client.name} joined the game as a hider.");
            } else {
                // you read this have a cookie 
            }
        }

        public override void OnPlayerDamaged(ClientInformation player, float damage, ClientInformation damager) {
            if (gameRunning && hiders.Contains(player)) {
                // Player was damaged by another player, so they are caught.
                hiders.Remove(player);
                Log.Info(NAME, $"{player.name} was caught by {damager.name}!");
            }
        }

        public override void OnPlayerQuit(ClientInformation client) {
            if (gameRunning) {
                if (client == seeker) {
                    // Handle the case where the seeker quits during the game.!
                    ModManager.serverInstance.SendReliableToAll(new DisplayTextPacket("game_seekerleft", "Seeker left choosing a new one", Color.yellow, Vector3.forward * 2, true, true, 10));
                    // Choose a random seeker from the hiders.
                    seeker = hiders[UnityEngine.Random.Range(0, hiders.Count)];
                    hiders.Remove(seeker);
                    Log.Info(NAME, $"{seeker.name} is the seeker!")
                    ModManager.serverInstance.SendReliableToAll(new DisplayTextPacket("game_newseeker", $"{seeker.name} is the new seeker", Color.yellow, Vector3.forward * 2, true, true, 10));
                } else if (hiders.Contains(client)) {
                    hiders.Remove(client);
                }
            }
        }

        public void StartGame() {
            if (hiders.Count >= 6) {
                gameRunning = true;

                // Choose a random seeker from the hiders.
                seeker = hiders[UnityEngine.Random.Range(0, hiders.Count)];
                hiders.Remove(seeker);

                Log.Info(NAME, $"{seeker.name} is the seeker!");
                ModManager.serverInstance.SendReliableToAll(new DisplayTextPacket("game_seekerchoosen", $"{seeker.name} Is the seeker. Hide now!", Color.yellow, Vector3.forward * 2, true, true, 10));

                // Start the hide time countdown
                StartCoroutine(HideTimeCountdown());

            } else {
                Log.Info(NAME, "Not enough players to start the game.");
            }
        }

        public void EndGame() {
            gameRunning = false;
            seeker = null;
            hiders.Clear();
            Log.Info(NAME, "Hide and Seek game ended.");
        }
    }
}
