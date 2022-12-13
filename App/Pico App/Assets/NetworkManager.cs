using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Threading;
using System.IO;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager INSTANCE;


    //UDP BROADCASTING
    const int UDP_PORT = 51519; 
    private int _iteration = 0; //Number of UDP sends
    private Thread _broadcastThread; //Thread managment
    private bool _response = false; //State variable
    private float _waitTime = 5; //Time between UDP sends

    //TRUE TCP CONNECTION
    const int TCP_PORT = 51520;
    private IPAddress _localAddress; //The local address
    private TcpListener _server = null; //The TCP server itself

    private byte[] _serverBuffer; //The receive buffer for the server
    private string _decodedData = null; //The decoded string
    private Thread _serverThread; //Thread managment

    //State variables
    private bool _networkingAlive;
    private bool _clientConnected = false;

    private bool _requestAllData = false;
    private bool _logging = true;
    private bool _resetBacklog = false;

    //The local data storage, populated from the pico through the network.
    private List<Measurment> _dataLog;

    private void Awake()
    {
        //This Singleton structure is not the best, dependency injection/managment is better.
        //But it does work well for single-scene, simple applications.
        INSTANCE = this;
    }

    void Start()
    {
        _dataLog = new List<Measurment>();

        //We first start the broadcast
        StartUDEBroadcast();

        //And we start the server
        StartServer();

        //At this point we have two simultanious threads for the UDP broadcast and the TCP server
    }


    void Update()
    {
        if( !_networkingAlive )
        {
            StartServer();
            StartUDEBroadcast();
        }
    }

    private void OnApplicationQuit()
    {
        //Clean up all possible running threads.
        if( _broadcastThread.IsAlive ) _broadcastThread.Abort();
        if( _serverThread.IsAlive ) _serverThread.Abort();
    }

    #region UDP Broadcast

    private void StartUDEBroadcast()
    {
        if(_broadcastThread == null || !_broadcastThread.IsAlive)
        {
            Debug.Log( "Starting Broadcast..." );
            _response = false;
            _broadcastThread = new Thread( new ThreadStart( SendBroadcast ) );
            _broadcastThread.Start();
        }
        else
        {
            Debug.LogError( "Attempted to start a second UDE broadcast thread. Ignoring..." );
        }

    }

    private void SendBroadcast()
    {
        //Wait until we get something on our main server thread.
        while( !_response )
        {
            _iteration++;

            UdpClient udp = new UdpClient( UDP_PORT );

            //Identifying the target as "this network" with the special maxed out 255.255.255.255 IP address endpoint.
            IPEndPoint ip = new IPEndPoint( IPAddress.Parse( "255.255.255.255" ), UDP_PORT );

            //Grab our custom broadcast message
            string msg = GetUdpMsg();
            Debug.Log( "Sending UDP Broadcast");
            byte[] encodedMsg = Encoding.ASCII.GetBytes( GetUdpMsg() );

            //Send it
            udp.Send( encodedMsg, encodedMsg.Length, ip );
            udp.Close();

            //We sleep to simply wait for the next broadcast
            //If we never slept and this code was used on a machine that is single processor or doesn't manage threads, then threading wouldn't work at all
            //Using a Thread.Sleep(0) would effectivley yield this thread to others for safer code, but an actual wait works too.
            Thread.Sleep( (int)(_waitTime * 1000) );
        }

        Debug.Log( "UDP Broadcast Halted." );
        _broadcastThread.Abort();
        _broadcastThread = null;
    }

    //Serializes our class defined below into a json formatted string.
    private string GetUdpMsg()
    {
        UDPMessage msg = new UDPMessage( _iteration, GetLocalIp(), TCP_PORT );
        string json = JsonConvert.SerializeObject( msg );

        return json;
    }

    private string GetLocalIp()
    {
        //The DNS class is a static class that retrieves DNS info, but it can also grab local info:

        string myName = Dns.GetHostName(); //Fetch my hostname from the DNS class.
        IPHostEntry myEntry = Dns.GetHostEntry( myName ); //Get the DNS entry for this local device. This phone.

        //This will iterate through all address that represent this computer. Usually it starts with all the IPv6
        //entries and the first IPv4 entry will be our target.
        foreach( var ip in myEntry.AddressList )
        {
            //If the entry is an IPv4 value!
            if (ip.AddressFamily == AddressFamily.InterNetwork )
            {
                return ip.ToString();
            }
        }
        Debug.LogError( "ERROR: This device has no working IPv4 interface." );
        return null;
    }

    #endregion

    #region TCP Server

    private void StartServer()
    {
        if( _serverThread == null || !_serverThread.IsAlive )
        {
            _serverThread = new Thread( new ThreadStart( RunServer ) );
            _serverThread.Start();
            _networkingAlive = true;
        }
    }

    private void RunServer()
    {
        try
        {
            _serverBuffer = new byte[85000];

            _localAddress = IPAddress.Parse( GetLocalIp() );

            Debug.Log( "Hosting Server On: " + _localAddress + ":" + TCP_PORT );
            //Starting the server
            _server = new TcpListener( _localAddress, TCP_PORT );
            _server.Server.ReceiveTimeout = 5000;
            _server.Start();

            while( true )
            {
                try
                {
                    _decodedData = null;
                    Debug.Log( "Waiting For Server Connection" );

                    //The "using" keyword incorperates resource cleanup into it automatically once it falls out of scope.
                    //This line is blocking \/
                    TcpClient client = _server.AcceptTcpClient();

                    _response = true; //Immediatley have the udp section halt.

                    Debug.Log( "Server Connected" );

                    //Grabbing the stream object
                    NetworkStream stm = client.GetStream();

                    int size;

                    while( client.Connected )
                    {
                        _clientConnected = true;
                        size = stm.Read( _serverBuffer, 0, _serverBuffer.Length );
                        if( size > 0 )
                        {
                            _decodedData = Encoding.ASCII.GetString( _serverBuffer );
                            Debug.Log( "Server Message Recevied: " + _decodedData );

                            ServerMessage msg = JsonConvert.DeserializeObject<ServerMessage>( _decodedData );

                            string response = ServerResponse( msg );
                            byte[] raw_resp = Encoding.ASCII.GetBytes( response );
                            stm.Write( raw_resp, 0, raw_resp.Length );

                            //Buffer needs to be cleared! Overflowing data breaks json decoding.
                            for( int i = 0; i < _serverBuffer.Length; i++ ) _serverBuffer[i] = 0;
                        }

                    }

                    //A Thread Yield.
                    Thread.Sleep( 0 );
                }
                catch( SocketException se )
                {
                    Debug.LogError( "Socket issue inside the running server " + se.Message + se.StackTrace);
                                   }
                catch( IOException ioe )
                {
                    Debug.LogError( "Could not read client socket, IOException, dropping client." + ioe.Message + ioe.StackTrace );
                }
                catch( Exception e )
                {
                    Debug.LogError( "There was an issue inside the server." + e.Message + e.StackTrace );
                }
                finally
                {
                    StartUDEBroadcast();
                    _clientConnected = false;
                }

            }

        }
        catch( SocketException se )
        {
            Debug.LogError( "There was an issue starting the server" );
        }
        finally
        {
            _server.Stop();
            _networkingAlive = false;
            _clientConnected = false;
        }
    }

    private string ServerResponse(ServerMessage incoming)
    {
        string response = "";
        switch( incoming.STATUS)
        {
            case "CONN":
                response = JsonConvert.SerializeObject( new ServerMessage( "ACK" ) );

                break;

            case "DATA":

                if( incoming.DATA != null && incoming.DATA.Count > 0 )
                {
                    _dataLog = incoming.DATA;
                    UIManager.INSTANCE.SetReading( _dataLog[_dataLog.Count - 1] );
                }

                if( !_requestAllData ) response = JsonConvert.SerializeObject( new ServerMessage( "ACK" ) );
                else
                {
                    _requestAllData = false;
                    response = JsonConvert.SerializeObject( new ServerMessage( "DATA" ) );
                }

                break;

            case "MES":

                if( _logging && incoming.DATA != null && incoming.DATA.Count > 0 ) {
                    _dataLog.Add( incoming.DATA[0] );
                    UIManager.INSTANCE.SetReading( incoming.DATA[0] ); 
                }

                if( !_requestAllData ) response = JsonConvert.SerializeObject( new ServerMessage( "ACK" ) );
                else if ( _resetBacklog )
                {
                    _resetBacklog = false;
                    response = JsonConvert.SerializeObject( new ServerMessage( "RST" ) );
                }
                else
                {
                    _requestAllData = false;
                    response = JsonConvert.SerializeObject( new ServerMessage( "DATA" ) );
                }

                break;
        }
        
        return response;
    }

    public bool IsServerConnected() { return _clientConnected; }

    public void ResetDataLogging()
    {
        _dataLog = new List<Measurment>();
    }
    public void DisableLogging()
    {
        _logging = !_logging;
    }

    public List<Measurment> GetDataLog()
    {
        return _dataLog; 
    }

    public void RequestAllData()
    {
        _dataLog = new List<Measurment>();
        _requestAllData = true;
    }

    public void ResetBacklog()
    {
        _resetBacklog = true;
    }

    #endregion
}

//This is the JSON serializable UDP message as a c# class
public class UDPMessage
{
    public string ID = "PicoCast"; //The broadcast ID
    public int iter; //The n-th iteration of trying to send this broadcast
    public string ip; //The app's IP!
    public int port; //The selected constant communication port.

    public UDPMessage(int it, string i, int p )
    {
        iter = it;
        ip = i;
        port = p;
    }
}

//This is the serializable TCP message sent back and fourth to/from the Pico/server in C# class form
[Serializable]
public class ServerMessage
{
    public string STATUS;
    public List<Measurment> DATA;

    [JsonConstructor]
    public ServerMessage(string s, List<Measurment> d )
    {
        STATUS = s;
        DATA = d;
    }
    public ServerMessage( string s )
    {
        STATUS = s;
        DATA = null;
    }
}

//A single measurment in C# class form
[Serializable]
public class Measurment
{
    public int TIME;
    public double TEMP;
    public double HUM;
    public double UV;

    [JsonConstructor]
    public Measurment(int ti, double t, double h, double u )
    {
        TIME = ti;
        TEMP = t;
        HUM = h;
        UV = u;
    }

    public override string ToString()
    {
        return TIME + " " + TEMP + " " + HUM + " " + UV;
    }
}