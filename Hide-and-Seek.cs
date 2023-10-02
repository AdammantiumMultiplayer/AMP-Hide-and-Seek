using AMP;
using AMP.DedicatedServer;
using AMP.DedicatedServer.Plugins;
using AMP.Events;
using AMP.Logging;
using AMP.Network.Data;
using AMP.Network.Packets.Implementation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Hide_and_seek
{
    public class HideAndSeek : AMP_Plugin
    {
        public override string NAME => "HideAndSeek";
        public override string AUTHOR => "Flexhd";
        public override string VERSION => "0.0.1";

        private bool gameRunning = false;
        private List<ClientData> hiders = new List<ClientData>();
        private List<ClientData> players = new List<ClientData>();
        private ClientData seeker = null;
        private float hideTime = 30.0f; // Hide time in seconds 
        HideAndSeekConfig config;

        private void HideTimeCountdown()
        {
            float countdown = hideTime; // Countdown time in seconds
            Thread.Sleep(10000);
            while (countdown > 0)
            {
                // Wait for 1 second
                countdown--;
                Log.Info(NAME, $"Countdown: {countdown} seconds");

                ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", $"Hide time ends in {countdown}!", Color.yellow, Vector3.forward * 2, true, true, 20)
                );
                Thread.Sleep(1000);
            }

            // Notify players that the game has started
            ModManager.serverInstance.netamiteServer.SendToAll(
                new DisplayTextPacket("say", "Hide and Seek game started!", Color.yellow, Vector3.forward * 2, true, true, 20)
            );
        }

        internal class HideAndSeekConfig : PluginConfig
        {
            public int REQUIRED_PLAYER_COUNT = 6;
            public float hideTime = 30.0f;
        }

        public override void OnStart()
        {
            Log.Info(NAME, "Hide and Seek plugin started.");
            ServerEvents.onPlayerQuit += OnPlayerQuit;
            ServerEvents.onPlayerJoin += OnPlayerJoin;
            ServerEvents.onPlayerDamaged += OnPlayerDamaged;
            config = (HideAndSeekConfig)GetConfig();
        }

        public void OnPlayerJoin(ClientData client)
        {
            if (!gameRunning)
            {
                hiders.Add(client);
                players.Add(client);
                Log.Info(NAME, $"{client.ClientName} joined the game as a hider.");
                client.SetOthersNametagVisibility(false);


            }
            else
            {
                string message = $"<color=#FFDC00>{client.ClientName} joined the Hide and seek match!\n{config.REQUIRED_PLAYER_COUNT - ModManager.serverInstance.connectedClients} still required to start.</color>";
                ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", message, Color.yellow, Vector3.forward * 2, true, true, 20)

                );
                Log.Info(NAME, $"{client.ClientName} joined the game ");
                players.Add(client);
                client.SetOthersNametagVisibility(false);
            }

            if (ModManager.serverInstance.connectedClients >= config.REQUIRED_PLAYER_COUNT)
            {
                Log.Info(NAME, $"{client.ClientName} joined the game and enaught people did join to start");
                client.SetOthersNametagVisibility(false);
                Thread Startgame = new Thread(StartGame);
                Startgame.Start();
                players.Add(client);
            }
        }

        public void OnPlayerDamaged(ClientData player, float damage, ClientData damager)
        {
            Log.Info(NAME, $"{player.ClientName} was caught by {damager.ClientName}!");
            if (gameRunning && hiders.Contains(player))
            {
                // Player was damaged by another player, so they are caught.
                hiders.Remove(player);
                Log.Info(NAME, $"{player.ClientName} was caught by {damager.ClientName}!");
                ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", $"{player.ClientName} was caught by {damager.ClientName}!", Color.yellow, Vector3.forward * 2, true, true, 20)
                );

                if (hiders.Count == 0)
                {
                    ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", "All players were caught, ending game.", Color.yellow, Vector3.forward * 2, true, true, 20)
                    );
                    
                    EndGame();
                }
            }
        }

        public void OnPlayerQuit(ClientData client)
        {
            if (gameRunning)
            {
                if (client == seeker)
                {
                    // Handle the case where the seeker quits during the game.
                    players.Remove(client);
                    ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", "Seeker left, choosing a new one.", Color.yellow, Vector3.forward * 2, true, true, 20)
                    );
                    // Choose a random seeker from the hiders.
                    System.Random random = new System.Random();
                    int randomIndex = random.Next(0, hiders.Count);
                    seeker = hiders[randomIndex];
                    hiders.RemoveAt(randomIndex);
                    
                    Log.Info(NAME, $"{seeker.ClientName} is the seeker!");
                    ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", $"{seeker.ClientName} is the new seeker.", Color.yellow, Vector3.forward * 2, true, true, 20)
                    );
                }
                else if (hiders.Contains(client))
                {
                    hiders.Remove(client);
                    players.Remove(client);
                }
                else if (gameRunning == false)
                {
                    players.Remove(client);
                }
            }
        }

        public void StartGame()
        {
            
            foreach (var Client in players)
            {
                hiders.Add(Client);
            }
           
            ModManager.serverInstance.netamiteServer.SendToAll(
            new DisplayTextPacket("say", "Seeker will be chosen in 10 seconds prepare", Color.yellow, Vector3.forward * 2, true, true, 20)
            );
            Thread.Sleep(10000);
            // Choose a random seeker from the hiders.
            System.Random random = new System.Random();
            int randomIndex = random.Next(0, hiders.Count);
            seeker = hiders[randomIndex];
            hiders.RemoveAt(randomIndex);
            Log.Info(NAME, $"{seeker.ClientName} is the seeker!");
            gameRunning = true;
            ModManager.serverInstance.netamiteServer.SendToAll(
            new DisplayTextPacket("say", $"{seeker.ClientName} Is the seeker. Hide now!", Color.yellow, Vector3.forward * 2, true, true, 20)
            );
            // Start the hide time countdown
            Thread timer = new Thread(HideTimeCountdown);
            timer.Start();
        }

        public void EndGame()
        {
            gameRunning = false;
            seeker = null;
            hiders.Clear();
            Log.Info(NAME, "Hide and Seek game ended.");
            ModManager.serverInstance.netamiteServer.SendToAll(
            new DisplayTextPacket("say", "Hide and seek game ended.", Color.yellow, Vector3.forward * 2, true, true, 20)
            );
        }
    }
}

