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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using UnityEngine;

namespace Hide_and_seek
{
    public class HideAndSeek : AMP_Plugin
    {
        public override string NAME => "HideAndSeek";
        public override string AUTHOR => "Flexhd";
        public override string VERSION => "1.0.0";

        private readonly object _lockObject = new object();
        private volatile bool _gameRunning = false;
        private volatile bool _gameStarted = false;
        private readonly List<ClientData> _hiders = new List<ClientData>();
        private ClientData _seeker = null;
        private HideAndSeekConfig _config;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _gameTask;
        private System.Timers.Timer _gameTimer;
        private System.Timers.Timer _delayTimer;
        private int _currentCountdown;
        private GamePhase _currentPhase;

        private enum GamePhase
        {
            Waiting,
            PreparationDelay,
            SeekerSelection,
            PreGameDelay,
            Hiding,
            Seeking,
            GameEndDelay,
            RestartDelay
        }

        internal class HideAndSeekConfig : PluginConfig
        {
            public int REQUIRED_PLAYER_COUNT = 6;
            public int hideTime = 30;
            public int seekTime = 300;
        }

        public HideAndSeek()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _currentPhase = GamePhase.Waiting;
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            _gameTimer = new System.Timers.Timer(1000); // 1 second interval
            _gameTimer.Elapsed += OnTimerElapsed;
            _gameTimer.AutoReset = true;

            _delayTimer = new System.Timers.Timer();
            _delayTimer.Elapsed += OnDelayTimerElapsed;
            _delayTimer.AutoReset = false; // One-shot timer
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_currentPhase != GamePhase.Hiding && _currentPhase != GamePhase.Seeking)
                        return;

                    _currentCountdown--;
                    HandleTimerTick();
                }
            }
            catch (Exception ex)
            {
                Log.Error(NAME, $"Timer error: {ex.Message}");
            }
        }

        private void OnDelayTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                lock (_lockObject)
                {
                    HandleDelayedAction();
                }
            }
            catch (Exception ex)
            {
                Log.Error(NAME, $"Delay timer error: {ex.Message}");
            }
        }

        private void HandleTimerTick()
        {
            switch (_currentPhase)
            {
                case GamePhase.Hiding:
                    HandleHidePhase();
                    break;
                case GamePhase.Seeking:
                    HandleSeekPhase();
                    break;
            }
        }

        private void HandleDelayedAction()
        {
            switch (_currentPhase)
            {
                case GamePhase.PreparationDelay:
                    StartSeekerSelection();
                    break;
                case GamePhase.SeekerSelection:
                    StartPreGameDelay();
                    break;
                case GamePhase.PreGameDelay:
                    StartHidePhase();
                    break;
                case GamePhase.GameEndDelay:
                    EndGame();
                    break;
                case GamePhase.RestartDelay:
                    RestartGame();
                    break;
            }
        }

        private void HandleHidePhase()
        {
            if (_currentCountdown > 0)
            {
                Log.Info(NAME, $"Hide countdown: {_currentCountdown} seconds");
                ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", $"Hide time ends\nin {_currentCountdown}!", Color.yellow, Vector3.forward * 2, true, true, 2)
                );
            }
            else
            {
                // Hide phase ended, start seek phase
                _currentPhase = GamePhase.Seeking;
                _currentCountdown = _config.seekTime;
                _gameRunning = true;

                _seeker?.ShowText("say", "Seek time started!", Color.yellow, 5);
                Log.Info(NAME, "Hide phase ended, seek phase started");
            }
        }

        private void HandleSeekPhase()
        {
            if (_currentCountdown > 0)
            {
                // Announce important milestones
                switch (_currentCountdown)
                {
                    case 90:
                        ModManager.serverInstance.netamiteServer.SendToAll(
                            new DisplayTextPacket("say", "Seek time ends\nin 1 minute 30 seconds!", Color.yellow, Vector3.forward * 2, true, true, 5)
                        );
                        break;
                    case 30:
                        ModManager.serverInstance.netamiteServer.SendToAll(
                            new DisplayTextPacket("say", "Seek time ends\nin 30 seconds!", Color.yellow, Vector3.forward * 2, true, true, 5)
                        );
                        break;
                    case var n when n <= 10:
                        ModManager.serverInstance.netamiteServer.SendToAll(
                            new DisplayTextPacket("say", $"Seek time ends\nin {_currentCountdown} seconds!", Color.yellow, Vector3.forward * 2, true, true, 5)
                        );
                        break;
                }

                Log.Info(NAME, $"Seek countdown: {_currentCountdown} seconds");
            }
            else
            {
                // Seek phase ended - hiders win
                ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", "Seek time over\nHiders win!", Color.yellow, Vector3.forward * 2, true, true, 5)
                );
                
                ScheduleDelayedAction(GamePhase.GameEndDelay, 5000);
            }
        }

        private void StartTimer(GamePhase phase, int duration)
        {
            lock (_lockObject)
            {
                _currentPhase = phase;
                _currentCountdown = duration;
                _gameTimer.Start();
            }
        }

        private void StopTimer()
        {
            _gameTimer?.Stop();
        }

        private void ScheduleDelayedAction(GamePhase phase, double delayMs)
        {
            lock (_lockObject)
            {
                _currentPhase = phase;
                _delayTimer.Interval = delayMs;
                _delayTimer.Start();
            }
        }






        public override void OnStart()
        {
            Log.Info(NAME, "Hide and Seek plugin started.");
            ServerEvents.onPlayerQuit += OnPlayerQuit;
            ServerEvents.onPlayerJoin += OnPlayerJoin;
            ServerEvents.onPlayerDamaged += OnPlayerDamaged;
            _config = (HideAndSeekConfig)GetConfig();
        }

        public override void OnStop()
        {
            ServerEvents.onPlayerQuit -= OnPlayerQuit;
            ServerEvents.onPlayerJoin -= OnPlayerJoin;
            ServerEvents.onPlayerDamaged -= OnPlayerDamaged;
            
            StopTimer();
            _gameTimer?.Dispose();
            _delayTimer?.Stop();
            _delayTimer?.Dispose();
            
            _cancellationTokenSource?.Cancel();
            _gameTask?.Wait(5000); // Wait up to 5 seconds for cleanup
            _cancellationTokenSource?.Dispose();
            
            Log.Info(NAME, "Hide and Seek plugin stopped.");
        }

        public void OnPlayerJoin(ClientData client)
        {
            lock (_lockObject)
            {
                if (_gameStarted)
                {
                    _hiders.Add(client);
                    Log.Info(NAME, $"{client.ClientName} joined the game as a hider.");
                }
                else
                {
                    int playersNeeded = _config.REQUIRED_PLAYER_COUNT - ModManager.serverInstance.connectedClients;
                    string message = $"<color=#FFDC00>{client.ClientName} joined\nthe Hide and Seek match!\n{playersNeeded} still required to start.</color>";
                    
                    ModManager.serverInstance.netamiteServer.SendToAll(
                        new DisplayTextPacket("say", message, Color.yellow, Vector3.forward * 2, true, true, 5)
                    );
                    
                    client.ShowText("say", $"Welcome to Hide and Seek\n{playersNeeded} players still required to start.", Color.yellow, 5);
                    Log.Info(NAME, $"{client.ClientName} joined the game");
                }

                client.SetOthersNametagVisibility(false);

                // Check if we have enough players to start
                if (!_gameStarted && ModManager.serverInstance.connectedClients >= _config.REQUIRED_PLAYER_COUNT)
                {
                    Log.Info(NAME, "Enough players joined to start the game");
                    StartGame();
                }
            }
        }

        public void OnPlayerDamaged(ClientData player, float damage, ClientData damager)
        {
            if (player == damager) return;

            lock (_lockObject)
            {
                if (_gameRunning && _hiders.Contains(player) && damager == _seeker)
                {
                    _hiders.Remove(player);
                    Log.Info(NAME, $"{player.ClientName} was caught by {damager.ClientName}!");
                    
                    ModManager.serverInstance.netamiteServer.SendToAll(
                        new DisplayTextPacket("say", $"{player.ClientName} was caught\nby {damager.ClientName}!", Color.yellow, Vector3.forward * 2, true, true, 8)
                    );
                    
                    Log.Info(NAME, $"Remaining hiders: {_hiders.Count}");

                    if (_hiders.Count == 0)
                    {
                        ModManager.serverInstance.netamiteServer.SendToAll(
                            new DisplayTextPacket("say", "All players were caught!\nSeeker wins!", Color.yellow, Vector3.forward * 2, true, true, 8)
                        );
                        
                        ScheduleDelayedAction(GamePhase.GameEndDelay, 3000);
                    }
                }
            }
        }

        public void OnPlayerQuit(ClientData client)
        {
            lock (_lockObject)
            {
                if (_gameStarted)
                {
                    if (client == _seeker)
                    {
                        ModManager.serverInstance.netamiteServer.SendToAll(
                            new DisplayTextPacket("say", "Seeker left,\nchoosing a new one.", Color.yellow, Vector3.forward * 2, true, true, 5)
                        );

                        // Choose a new seeker from remaining players
                        var availablePlayers = ModManager.serverInstance.Clients.Where(c => c != client).ToArray();
                        if (availablePlayers.Length > 0)
                        {
                            var random = new System.Random();
                            _seeker = availablePlayers[random.Next(availablePlayers.Length)];
                            _hiders.Remove(_seeker);

                            Log.Info(NAME, $"{_seeker.ClientName} is the new seeker!");
                            ModManager.serverInstance.netamiteServer.SendToAll(
                                new DisplayTextPacket("say", $"{_seeker.ClientName}\nis the new seeker.", Color.yellow, Vector3.forward * 2, true, true, 5)
                            );
                        }
                        else
                        {
                            // Not enough players, end the game
                            EndGame();
                        }
                    }
                    else
                    {
                        _hiders.Remove(client);
                        
                        // Check if game should end due to insufficient players
                        if (_hiders.Count == 0 && _seeker != null)
                        {
                            EndGame();
                        }
                    }
                }
            }
        }
        private void StartGame()
        {
            lock (_lockObject)
            {
                if (_gameStarted) return; // Prevent multiple starts
                _gameStarted = true;
                _gameRunning = false;
                _hiders.Clear();
                _hiders.AddRange(ModManager.serverInstance.Clients);
            }

            ScheduleDelayedAction(GamePhase.PreparationDelay, 5000);
        }

        private void StartSeekerSelection()
        {
            ModManager.serverInstance.netamiteServer.SendToAll(
                new DisplayTextPacket("say", "Seeker will be chosen\nin 10 seconds, prepare!", Color.yellow, Vector3.forward * 2, true, true, 10)
            );

            ScheduleDelayedAction(GamePhase.SeekerSelection, 10000);
        }

        private void StartPreGameDelay()
        {
            lock (_lockObject)
            {
                if (_hiders.Count == 0) 
                {
                    EndGame();
                    return;
                }

                var random = new Random();
                int randomIndex = random.Next(_hiders.Count);
                _seeker = _hiders[randomIndex];
                _hiders.Remove(_seeker);
            }

            Log.Info(NAME, $"{_seeker.ClientName} is the seeker!");
            Log.Info(NAME, $"Hiders: {_hiders.Count}");

            ModManager.serverInstance.netamiteServer.SendToAll(
                new DisplayTextPacket("say", $"{_seeker.ClientName} is the seeker.\nHide now!", Color.yellow, Vector3.forward * 2, true, true, 10)
            );

            _seeker.SetOthersNametagVisibility(false);
            _seeker.ShowText("say", $"You are the Seeker\nHiders have {_config.hideTime + 20} seconds to hide", Color.yellow, 15);

            ScheduleDelayedAction(GamePhase.PreGameDelay, 10000);
        }

        private void StartHidePhase()
        {
            StartTimer(GamePhase.Hiding, _config.hideTime);
        }

        private void EndGame()
        {
            StopTimer();

            lock (_lockObject)
            {
                _seeker?.SetOthersNametagVisibility(true);
                _gameRunning = false;
                _gameStarted = false;
                _seeker = null;
                _hiders.Clear();
            }

            Log.Info(NAME, "Hide and Seek game ended.");

            if (ModManager.serverInstance.connectedClients >= _config.REQUIRED_PLAYER_COUNT)
            {
                ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", "Hide and Seek game ended.\nStarting new one in 20 seconds", Color.yellow, Vector3.forward * 2, true, true, 15)
                );

                ScheduleDelayedAction(GamePhase.RestartDelay, 20000);
            }
            else
            {
                ModManager.serverInstance.netamiteServer.SendToAll(
                    new DisplayTextPacket("say", "Not enough players\nfor another round.", Color.yellow, Vector3.forward * 2, true, true, 10)
                );
                
                lock (_lockObject)
                {
                    _currentPhase = GamePhase.Waiting;
                }
            }
        }

        private void RestartGame()
        {
            if (ModManager.serverInstance.connectedClients >= _config.REQUIRED_PLAYER_COUNT)
            {
                lock (_lockObject)
                {
                    _currentPhase = GamePhase.Waiting;
                }
                StartGame();
            }
            else
            {
                lock (_lockObject)
                {
                    _currentPhase = GamePhase.Waiting;
                }
            }
        }
    }
}


