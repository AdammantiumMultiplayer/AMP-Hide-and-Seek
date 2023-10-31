using AMP;
using AMP.DedicatedServer;
using AMP.DedicatedServer.Plugins;
using AMP.Events;
using AMP.Logging;
using AMP.Network.Data;
using AMP.Network.Packets.Implementation;
using Netamite.Client.Implementation;
using Netamite.Network.Packet.Implementations;
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
        public override string VERSION => "0.5.2";

        private bool gameRunning = false;
        private bool gameStarted = false;
        private List<ClientData> hiders = new List<ClientData>();
        private ClientData seeker = null;// Hide time in seconds 
        HideAndSeekConfig config;

        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;


        internal class HideAndSeekConfig : PluginConfig
        {
            public int REQUIRED_PLAYER_COUNT = 6;
            public float hideTime = 30.0f;
            public float seekTime = 300.0f;
        }

        public HideAndSeek()
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
        }
        public void ResetCancellationToken()
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
        }

        private void HideTimeCountdown()
        {
            float countdown = config.hideTime; // Countdown time in seconds
            Thread.Sleep(10000);
            while (countdown > 0)
            {
                // Wait for 1 second
                countdown--;
                Log.Info(NAME, $"Countdown: {countdown} seconds");

                ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", $"Hide time ends \nin {countdown}!", Color.yellow, Vector3.forward * 2, true, true, 2)
                );
                Thread.Sleep(1000);

            }
            if (countdown == 0)
            {
                seeker.ShowText("say", $"Seek time Started", Color.yellow, 5);                
                Thread.Sleep(1000);
                Thread seektimes = new Thread(() => seekTimer(cancellationToken));
                seektimes.Start();
                gameRunning = true;
            }
        }


        private void seekTimer(CancellationToken cancellationToken)
        {
            float countdownseek = config.seekTime;
            while (countdownseek > 0 && !cancellationToken.IsCancellationRequested)
            {
                // Wait for 1 second
                countdownseek--;
                // Check if the remaining time is 1 minute 30 seconds, 15 seconds, or less than 10 seconds
                if (countdownseek == 90)
                {
                    Log.Info(NAME, $"Countdown: 1 minute 30 seconds");
                    ModManager.serverInstance.netamiteServer.SendToAll(
                        new DisplayTextPacket("say", $"Seek time ends \nin 1 minute 30 seconds!", Color.yellow, Vector3.forward * 2, true, true, 5)
                    );
                }
                else if (countdownseek == 30)
                {
                    Log.Info(NAME, $"Countdown: 30 seconds");
                    ModManager.serverInstance.netamiteServer.SendToAll(
                        new DisplayTextPacket("say", $"Seek time ends \nin 30 seconds!", Color.yellow, Vector3.forward * 2, true, true, 5)
                    );
                }
                else if (countdownseek <= 10)
                {
                    Log.Info(NAME, $"Countdown: {countdownseek} seconds");
                    ModManager.serverInstance.netamiteServer.SendToAll(
                        new DisplayTextPacket("say", $"Seek time ends \nin {countdownseek} seconds!", Color.yellow, Vector3.forward * 2, true, true, 5)
                    );
                }
                else
                {
                    Log.Info(NAME, $"Countdown: {countdownseek} seconds");
                }
                if (countdownseek == 0)
                {
                    ModManager.serverInstance.netamiteServer.SendToAll(
                        new DisplayTextPacket("say", $"Seek times over \n Hiders win", Color.yellow, Vector3.forward * 2, true, true, 5)
                    );
                    Thread.Sleep(5000);
                    Thread EndGames = new Thread(EndGame);
                    EndGames.Start();
                }
                Thread.Sleep(1000);
            }
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
            if (gameStarted)
            {
                hiders.Add(client);
                Log.Info(NAME, $"{client.ClientName} joined the game as a hider.");
                client.SetOthersNametagVisibility(false);


            }
            else
            {
                string message = $"<color=#FFDC00>{client.ClientName} joined \nthe Hide and seek match!\n{config.REQUIRED_PLAYER_COUNT - ModManager.serverInstance.connectedClients} still required to start.</color>";
                ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", message, Color.yellow, Vector3.forward * 2, true, true, 5)


                );
                client.ShowText("say", $"Welcome to Hide and Seek \n{config.REQUIRED_PLAYER_COUNT - ModManager.serverInstance.connectedClients} players still required to start.", Color.yellow, 5);
                Log.Info(NAME, $"{client.ClientName} joined the game ");
                client.SetOthersNametagVisibility(false);
            }

            if (ModManager.serverInstance.connectedClients >= config.REQUIRED_PLAYER_COUNT)
            {
                Log.Info(NAME, $"{client.ClientName} joined the game and enaught people did join to start");
                client.SetOthersNametagVisibility(false);
                Thread Startgame = new Thread(StartGame);
                Startgame.Start();
            }
        }

        public void OnPlayerDamaged(ClientData player, float damage, ClientData damager)
        {
            if (player == damager)
            {
                return;
            }

            if (gameRunning && hiders.Contains(player) && damager == seeker)
            {
                // Player was damaged by another player, so they are caught.
                hiders.Remove(player);
                Log.Info(NAME, $"{player.ClientName} was caught by {damager.ClientName}!");
                ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", $"{player.ClientName} was caught\n by {damager.ClientName}!", Color.yellow, Vector3.forward * 2, true, true, 8)
                );
                Log.Info(NAME, $"hiders:{hiders.Count} ");

                if (hiders.Count == 0)
                {
                    ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", "All players were caught, \nending game.", Color.yellow, Vector3.forward * 2, true, true, 8)
                    );
                    Thread EndGames = new Thread(EndGame);
                    EndGames.Start();
                    cancellationTokenSource.Cancel();
                }
            }
        }

        public void OnPlayerQuit(ClientData client)
        {
            if (gameStarted)
            {
                if (client == seeker)
                {
                    // Handle the case where the seeker quits during the game.
                    ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", "Seeker left, \nchoosing a new one.", Color.yellow, Vector3.forward * 2, true, true, 20)
                    );
                    // Choose a random seeker from the hiders.
                    System.Random random = new System.Random();
                    int randomIndex = random.Next(0, ModManager.serverInstance.Clients.Length);
                    seeker = ModManager.serverInstance.Clients[randomIndex];
                    hiders.Remove(seeker);



                    Log.Info(NAME, $"{seeker.ClientName} is the seeker!");
                    ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", $"{seeker.ClientName} \nis the new seeker.", Color.yellow, Vector3.forward * 2, true, true, 20)
                    );
                }
                else if (hiders.Contains(client))
                {
                    hiders.Remove(client);
                }
            }
        }
        public void StartGame()
        {
            gameStarted = true;
            hiders.Clear();
            Thread.Sleep(5000);
            foreach (var Client in ModManager.serverInstance.Clients)
            {
                hiders.Add(Client);
            }
           
            ModManager.serverInstance.netamiteServer.SendToAll(
            new DisplayTextPacket("say", "Seeker will be chosen \nin 10 seconds prepare", Color.yellow, Vector3.forward * 2, true, true, 20)
            );
            
            Thread.Sleep(10000);
            // Choose a random seeker from the hiders.
            // Choose a random seeker from the hiders.
            System.Random random = new System.Random();
            int randomIndex = random.Next(0, ModManager.serverInstance.Clients.Length);
            seeker = ModManager.serverInstance.Clients[randomIndex];
            hiders.Remove(seeker);
            Log.Info(NAME, $"{seeker.ClientName} is the seeker!");
            Log.Info(NAME, $"hiders:{hiders.Count} ");
            

            
            ModManager.serverInstance.netamiteServer.SendToAll(
            new DisplayTextPacket("say", $"{seeker.ClientName} Is the seeker. Hide now!", Color.yellow, Vector3.forward * 2, true, true, 20)
            );
            seeker.SetOthersNametagVisibility(false);
            seeker.ShowText("say", $"You are the Seeker \n The Hiders have {config.hideTime + 20} Seconds to hide", Color.yellow, 20);
            // Start the hide time countdown
            Thread.Sleep(10000);
            ResetCancellationToken();
            Thread timer = new Thread(HideTimeCountdown);
            timer.Start();
        }

        public void EndGame()
        {
            seeker.SetOthersNametagVisibility(true);
            gameRunning = false;
            gameStarted = false;
            seeker = null;
            hiders.Clear();
            Log.Info(NAME, "Hide and Seek game ended.");
            if (ModManager.serverInstance.connectedClients >= config.REQUIRED_PLAYER_COUNT)
            {
                ModManager.serverInstance.netamiteServer.SendToAll(
                new DisplayTextPacket("say", "Hide and seek game ended.\n Starting new one in 20 seconds", Color.yellow, Vector3.forward * 2, true, true, 20)
                );
                Thread.Sleep(20000);
                Thread Startgame = new Thread(StartGame);
                Startgame.Start();
            }
            
        }
    }
}


