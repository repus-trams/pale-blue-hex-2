using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkManagerPBH : NetworkManager
{
    //VERY USEFUL FOR SCENE CHANGING
    //REFRENCES SCENE by name by dragging it in through the inspector
    [SerializeField] private int minPlayers = 2;

    [Scene] [SerializeField] private string menuScene = string.Empty;

    [Header("Room")]
    [SerializeField] private NetworkRoomPlayerPBH roomPlayerPrefab;//the lobby player prefab

    [Header("Game")]
    [SerializeField] private NetworkGamePlayerPBH gamePlayerPrefab;
    [SerializeField] private GameObject roundSystem;

    public static event Action OnClientConnected;
    public static event Action OnClientDisconnected;

    public List<NetworkRoomPlayerPBH> RoomPlayers { get; } = new List<NetworkRoomPlayerPBH>();
    public List<NetworkGamePlayerPBH> GamePlayers { get; } = new List<NetworkGamePlayerPBH>();


    //these two just make sure all gameobjects are under the spawnable prefabs so we dont have to add them manually
    public override void OnStartServer() => spawnPrefabs = Resources.LoadAll<GameObject>("SpawnablePrefabs").ToList();

    public override void OnStartClient()
    {
        var spawnablePrefabs = Resources.LoadAll<GameObject>("SpawnablePrefabs");

        foreach(var prefab in spawnablePrefabs)
        {
            NetworkClient.RegisterPrefab(prefab);
        }
    }

    
    //if client connects do the normal stuff and ping the event
    public override void OnClientConnect(NetworkConnection conn)
    {
        base.OnClientConnect(conn);

        OnClientConnected?.Invoke();
    }
    //if client disconnects - same
    public override void OnClientDisconnect(NetworkConnection conn)
    {
        base.OnClientDisconnect(conn);

        OnClientDisconnected?.Invoke();
    }

    public override void OnServerConnect(NetworkConnection conn)
    {
        //If too many players are already connected - disconnect
        if(numPlayers >= maxConnections)
        {
            conn.Disconnect();
            return;
        }
        //if we are trying to connect to the wrong scene (ie not in lobby) - disconnect
        if("Assets/Scenes/" + SceneManager.GetActiveScene().name + ".unity" != menuScene)
        {
            conn.Disconnect();
            return;
        }
    }

    public override void OnServerAddPlayer(NetworkConnection conn)
    {
        if ("Assets/Scenes/" + SceneManager.GetActiveScene().name + ".unity" == menuScene)//if in the lobby
        {
            bool isLeader = RoomPlayers.Count == 0;

            NetworkRoomPlayerPBH roomPlayerInstance = Instantiate(roomPlayerPrefab);//add an instance of the room player prefab

            roomPlayerInstance.IsLeader = isLeader;

            NetworkServer.AddPlayerForConnection(conn, roomPlayerInstance.gameObject);//tie together the connection and the gameobject
        }
    }

    public override void OnServerDisconnect(NetworkConnection conn)
    {
        if(conn.identity != null)
        {
            var player = conn.identity.GetComponent<NetworkRoomPlayerPBH>();
            //if a client disconnects remove them from the player list
            RoomPlayers.Remove(player);

            NotifyPlayersOfReadyState();
        }

        base.OnServerDisconnect(conn);
    }

    public override void OnStopServer()
    {
        RoomPlayers.Clear();
        GamePlayers.Clear();
    }

    public void NotifyPlayersOfReadyState()
    {
        foreach(var player in RoomPlayers)
        {
            player.HandleReadyToStart(IsReadyToStart());
        }
    }

    private bool IsReadyToStart()
    {
        if (numPlayers < minPlayers) { return false; }

        foreach(var player in RoomPlayers)
        {
            if (!player.IsReady) { return false; }
        }

        return true;
    }

    public void StartGame()
    {
        if(SceneManager.GetActiveScene().path == menuScene)
        {
            Debug.Log("Starting!");

            if (!IsReadyToStart())  { return; }

            ServerChangeScene("Scene_Map_01");
        }
    }

    public override void ServerChangeScene(string newSceneName)
    {
        if(SceneManager.GetActiveScene().path == menuScene && newSceneName.StartsWith("Scene_Map"))
        {
            for(int i = RoomPlayers.Count - 1; i >= 0; i--)
            {
                var conn = RoomPlayers[i].connectionToClient;
                var gameplayerInstance = Instantiate(gamePlayerPrefab);
                gameplayerInstance.SetDisplayName(RoomPlayers[i].DisplayName);

                NetworkServer.Destroy(conn.identity.gameObject);

                NetworkServer.ReplacePlayerForConnection(conn, gameplayerInstance.gameObject);
            }
        }

        base.ServerChangeScene(newSceneName);
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        if (sceneName.StartsWith("Scene_Map"))
        {
            GameObject roundSystemInstance = Instantiate(roundSystem);
            NetworkServer.Spawn(roundSystemInstance);
        }

        base.OnServerSceneChanged(sceneName);
    }
}
