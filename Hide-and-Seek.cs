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
        public override string VERSION { get { return "0.0.1"; } }
        public override string AUTHOR  { get { return "flexhd""; } }

        private bool gameRunning = false;
        private List<ClientData> hiders = new List<ClientData>();
        private List<ClientData> players = new List<ClientData>();
        private ClientData seeker = null;
        private float hideTime = 30.0f; // Hide time in seconds
        private static int REQUIRED_PLAYER_COUNT = 6;
        private IEnumerator HideTimeCountdown()
        {
            float countdown = 30.0f; // Countdown time in seconds

            while (countdown > 0)
            {
                yield return new WaitForSeconds(1.0f); // Wait for 1 second
                countdown--;
                Log.Info(NAME, $"Countdown: {countdown} seconds");
                ModManager.serverInstance.netamiteServer.SendToAll(
                new DisplayTextPacket("game_countdown", String.Join($"Hide time ends in {countdown}!", args), Color.yellow, Vector3.forward * 2, true, true, 20)
            );
            }

            // Notify players that the game has started
            ModManager.serverInstance.netamiteServer.SendToAll(
                new DisplayTextPacket("say", String.Join("Hide and Seek game started!", args), Color.yellow, Vector3.forward * 2, true, true, 20)
            );
        }





        public override void OnStart() {
            Log.Info(NAME, "Hide and Seek plugin started.");
        }

        public override void OnPlayerJoin(ClientData client) {
            if (!gameRunning) {
                //hiders.Add(client);
                players.Add(client);
                Log.Info(NAME, $"{client.name} joined the game as a hider.");
            } else {
                message = $"<color=#FFDC00>{client.name} joined the Hide and seek match!\n{REQUIRED_PLAYER_COUNT - ModManager.serverInstance.connectedClients} still required to start.</color>";
                ModManager.serverInstance.netamiteServer.SendToAll(
                new DisplayTextPacket("say", String.Join(message, args), Color.yellow, Vector3.forward * 2, true, true, 20)
                );
                players.Add(client);

            } else if (ModManager.serverInstance.connectedClients >= REQUIRED_PLAYER_COUNT) {
                StartGame()
            );
 
            }
        }

        public override void OnPlayerDamaged(ClientData player, float damage, ClientInformation damager) {
            if (gameRunning && hiders.Contains(player)) {
                // Player was damaged by another player, so they are caught.
                hiders.Remove(player);
                Log.Info(NAME, $"{player.name} was caught by {damager.name}!");
                ModManager.serverInstance.netamiteServer.SendToAll(
                new DisplayTextPacket("say", String.Join($"{player.name} was caught by {damager.name}!", args), Color.yellow, Vector3.forward * 2, true, true, 20)
                )

            if (hiders.Count = 0) {
                ModManager.serverInstance.netamiteServer.SendToAll(
                new DisplayTextPacket("say", String.Join("all players where caught ending game", args), Color.yellow, Vector3.forward * 2, true, true, 20)
                )
                EndGame()

            }
        }

        public override void OnPlayerQuit(ClientData client) {
            if (gameRunning) {
                if (client == seeker) {
                    // Handle the case where the seeker quits during the game.!
                    players.Remove(client);
                    ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", String.Join("Seeker left choosing a new one", args), Color.yellow, Vector3.forward * 2, true, true, 20)
                    );
                    // Choose a random seeker from the hiders.
                    seeker = hiders[UnityEngine.Random.Range(0, hiders.Count)];
                    hiders.Remove(seeker);
                    Log.Info(NAME, $"{seeker.name} is the seeker!")
                    ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", String.Join($"{seeker.name} is the new seeker", args), Color.yellow, Vector3.forward * 2, true, true, 20)
                    );
                } else if (hiders.Contains(client)) {
                    hiders.Remove(client);
                    players.Remove(client);
                }
            }
        }

        public void StartGame() {
                gameRunning = true;
                foreach (var ClientId in players){
                        hiders.add(ClientId);
                }

                // Choose a random seeker from the hiders.
                seeker = hiders[UnityEngine.Random.Range(0, hiders.Count)];
                hiders.Remove(seeker);

                Log.Info(NAME, $"{seeker.name} is the seeker!");
                //ModManager.serverInstance.SendReliableToAll(new DisplayTextPacket("game_seekerchoosen", $"{seeker.name} Is the seeker. Hide now!", Color.yellow, Vector3.forward * 2, true, true, 10));
                ModManager.serverInstance.netamiteServer.SendToAll(
                new DisplayTextPacket("say", String.Join($"{seeker.name} Is the seeker. Hide now!", args), Color.yellow, Vector3.forward * 2, true, true, 20)
                );
                // Start the hide time countdown
                StartCoroutine(HideTimeCountdown());

            
            }
        }

        public void EndGame() {
            gameRunning = false;
            seeker = null;
            hiders.Clear();
            Log.Info(NAME, "Hide and Seek game ended.");
            ModManager.serverInstance.netamiteServer.SendToAll(
            new DisplayTextPacket("say", String.Join("Hide and seek game ended", args), Color.yellow, Vector3.forward * 2, true, true, 20)
            );
        }
    }
}
