using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shapes;

public class UIManager : MonoBehaviour
{
    public static UIManager INSTANCE;

    //All reading objects
    [SerializeField] private Text _lastDataTime;
    [SerializeField] private Text _tempReading;
    [SerializeField] private Text _humidityReading;
    [SerializeField] private Text _lastUVReading;
    [SerializeField] private Text _lastBatteryReading;

    [Space]
    //Connection objects
    [SerializeField] private Text _connectionStatus;
    [SerializeField] private Disc _connectionIndicator;

    //State variables and the most recent reading to populate to UI
    private bool _newReading = false;
    private Measurment _measurment = null;
    private Measurment _lastMeasurment = null;
    private float _lastRealTime;

    //GRAPH DATA
    [SerializeField] private GameObject _graphNodePrefab;
    private Color _tempColor = Color.red;
    private Color _humColor = Color.green;
    private Color _uvColor = Color.magenta;
    private List<GameObject> _tempMarkers;
    private List<GameObject> _humMarkers;
    private List<GameObject> _uvMarkers;
    [SerializeField] private int _maxGraphMarkers = 200;
    [SerializeField] private Transform _canvas;
    [Space]
    [SerializeField] private GameObject _graphTempOrigin;
    [SerializeField] private GameObject _graphTempXMax;
    [SerializeField] private GameObject _graphTempYMax;

    [SerializeField] private GameObject _graphUVOrigin;
    [SerializeField] private GameObject _graphUVXMax;
    [SerializeField] private GameObject _graphUVYMax;

    //Larger than any screen could be. i.e: out of view for safekeeping the graph points.
    private static Vector2 HOLDING = new Vector2( -5000, -5000 ); 

    private void Awake()
    {
        //This Singleton structure is not the best, dependency injection/managment is better.
        //But it does work well for single-scene, simple applications.
        INSTANCE = this;
    }

    void Start()
    {
        //Pool all the markers used for graphing, much more preformant than relying on
        //The Unity Engine garbage collection to handle runtime instantiation/deletion
        _tempMarkers = new List<GameObject>();
        _humMarkers = new List<GameObject>();
        _uvMarkers = new List<GameObject>();
        for(int i = 0; i < _maxGraphMarkers; i++ )
        {
            var _tempMarker = Instantiate( _graphNodePrefab, _canvas );
            var tempComponent = _tempMarker.GetComponent<Disc>();
            tempComponent.Color = _tempColor;
            _tempMarker.transform.position = HOLDING;
            _tempMarkers.Add( _tempMarker );

            var _humMarker = Instantiate( _graphNodePrefab, _canvas );
            var humComponent = _humMarker.GetComponent<Disc>();
            humComponent.Color = _humColor;
            _humMarker.transform.position = HOLDING;
            _humMarkers.Add( _humMarker );

            var _uvMarker = Instantiate( _graphNodePrefab, _canvas );
            var uvComponent = _uvMarker.GetComponent<Disc>();
            uvComponent.Color = _uvColor;
            _uvMarker.transform.position = HOLDING;
            _uvMarkers.Add( _uvMarker );
        }
    }


    void Update()
    {
        if( _measurment != null ) SetMostRecentReading( ); //If we have a reading then set the reading and nullify the variable
        else { UpdateReadingTime(); } //Otherwise just continue to keep time since the last reading.

        UpdateConnectivityStatus(); //Update the small connection status indicator

        UpdateGraphs(); //update graphs
    }

    #region Buttons

    //Every function in this region is triggered only by a Unity event when a certain linked button is pressed by the user.

    public void RequestData()
    {
        NetworkManager.INSTANCE.RequestAllData();
        _lastBatteryReading.text = "Waiting . . .";
    }

    public void ToggleLogging()
    {
        NetworkManager.INSTANCE.DisableLogging();
    }

    public void ResetData()
    {
        NetworkManager.INSTANCE.ResetDataLogging();
        _lastBatteryReading.text = "No Datapoints";
    }

    public void ResetBacklog()
    {
        NetworkManager.INSTANCE.ResetBacklog();
    }

    #endregion

    #region UI Updates

    //Each function modifies the UI in some way

    //This function sets a new measurment to update the UI. The UI can not be updated directly
    //By the network server because Unity interfaces can only be used my the main thread, and
    //the server is never on the main thread.
    public void SetReading( Measurment measurment ) 
    { 
        _measurment = measurment;
    }

    public void SetMostRecentReading()
    {
        if( _measurment != null )
        {
            List<Measurment> all_mea = NetworkManager.INSTANCE.GetDataLog();

            _lastRealTime = Time.time;
            _lastDataTime.text = "(0s Ago)";
            _tempReading.text = "(" + ( ( int )( _measurment.TEMP * 100 ) / 100.0f ) + " C)";
            _humidityReading.text = "(" + ( ( int )( _measurment.HUM * 100 ) / 100.0f ) + "%)";
            _lastUVReading.text = "(" + ( ( int )( _measurment.UV * 100 ) / 100.0f ) + " mW/m^2)";

            if(all_mea.Count > 1) _lastBatteryReading.text = all_mea.Count + " Datapoints";
            else if(all_mea.Count == 1) _lastBatteryReading.text = all_mea.Count + " Datapoint";
            else _lastBatteryReading.text = "No Datapoints";

            _lastMeasurment = _measurment;
            _measurment = null;
        }
    }

    public void UpdateReadingTime() 
    {
        _lastDataTime.text = "(" + ( ( int )(Time.time - _lastRealTime)) + "s Ago)";
    }

    public void UpdateConnectivityStatus()
    {
        if( NetworkManager.INSTANCE.IsServerConnected() )
        {
            _connectionStatus.text = "Connected";
            _connectionIndicator.Color = Color.green;
        }
        else
        {
            _connectionStatus.text = "Disconnected";
            _connectionIndicator.Color = Color.red;
            _lastBatteryReading.text = "";
        }

    }

    private void UpdateGraphs()
    {
        //reset graph markers
        for(int i = 0; i < _maxGraphMarkers; i++ )
        {
            _tempMarkers[i].transform.position = HOLDING;
            _humMarkers[i].transform.position = HOLDING;
            _uvMarkers[i].transform.position = HOLDING;

        }

        //Get all data to graph
        List<Measurment> all_mea = NetworkManager.INSTANCE.GetDataLog();

        if( all_mea.Count > 2 )
        {
            //Get time bounds
            int earliest_time = 100000000;
            int latest_time = 0;
            for( int i = 0; i < all_mea.Count; i++ )
            {
                if( all_mea[i].TIME < earliest_time )
                    earliest_time = all_mea[i].TIME;
                if( all_mea[i].TIME > latest_time )
                    latest_time = all_mea[i].TIME;
            }

            //calculate graph bounds
            int timeDiff = latest_time - earliest_time;

            float tempYMax = 32;
            float humidityYMax = 100;
            float uvYMax = 12;

            Vector3 tempXSize = _graphTempXMax.transform.position - _graphTempOrigin.transform.position;
            Vector3 tempYSize = _graphTempYMax.transform.position - _graphTempOrigin.transform.position;
            Vector3 UVXSize = _graphUVXMax.transform.position - _graphUVOrigin.transform.position;
            Vector3 UVYSize = _graphUVYMax.transform.position - _graphUVOrigin.transform.position;

            //Set the position of each marker based on it's corresponding reading
            //The measurment array is parallel to all three different graph dot arrays
            for( int i = 0; i < all_mea.Count; i++ )
            {
                Measurment m = all_mea[i];

                //TEMP FIRST
                //double xVal = ( ( m.TIME - earliest_time ) / ((float)timeDiff) ) * tempXSize.x + _graphTempOrigin.transform.position.x;
                double xVal = ((float)(i+1) / all_mea.Count) * tempXSize.x + _graphTempOrigin.transform.position.x - ( ( float )1 / (2.0f * all_mea.Count) );
                double yVal = ( ( m.TEMP ) / tempYMax ) * tempYSize.y + _graphTempOrigin.transform.position.y;
                _tempMarkers[i].transform.position = new Vector3( ( float )xVal, ( float )yVal, 0 );

                //HUMDITY
                yVal = ( ( m.HUM ) / humidityYMax ) * tempYSize.y + _graphTempOrigin.transform.position.y;
                _humMarkers[i].transform.position = new Vector3( ( float )xVal, ( float )yVal, 0 );

                //UV
                //xVal = ( ( m.TIME - earliest_time ) / ( ( float )timeDiff ) ) * UVXSize.x + _graphUVOrigin.transform.position.x;
                xVal = ( ( float )( i + 1 ) / all_mea.Count ) * UVXSize.x + _graphUVOrigin.transform.position.x - ( ( float )1 / ( 2.0f * all_mea.Count ) );
                yVal = ( ( m.UV ) / uvYMax ) * UVYSize.y + _graphUVOrigin.transform.position.y;
                _uvMarkers[i].transform.position = new Vector3( ( float )xVal, ( float )yVal, 0 );
            }
        }


    }

    #endregion

}
