from machine import Pin, SPI, I2C, ADC, PWM, UART #pico libs needed
import math #mathematics needed
import time #time for waiting, but actual time measurments don't wokr on pico.
import random
import socket #this is sockets !
import json #this is for json manipulation
import secret #This contains my home network credentials
import ds1307 #The RTC
import dht #The temp sensor.
import _thread #Threading

import network

led = Pin("LED", Pin.OUT) #This is the LED ON the Pico board

rtc = None #REAL TIME CLOCK
temp = None #TEMP SENSOR DHT22
uv = None #UV SENSOR ML8511
vref = None #UV SENSOR VOLTAGE REFRENCE

initial_time = -1 #TIME RECORDING

all_readings = [] #ALL BACKGROUND READINGS

binded = True #STATE VARIABLE

#SETTINGS FOR EMASURING BACKGROUND SENSOR INTERVALS
background_measure_counter = 0
background_measure_thresh = 15

def main():

    global rtc
    global temp
    global uv
    global vref
    global binded

    global initial_time

    #INITALIZATION STEPS FIRST

    #THE REAL TIME CLOCK
    i2c = I2C(id=0, sda=Pin(8), scl=Pin(9))
    rtc = ds1307.DS1307(i2c)
    rtc.datetime((2000,0,0,0,0,0,0,0))
    initial_time = rtc.datetime_seconds()

    #THE TEMP SENSOR
    temp = dht.DHT22(Pin(2))

    #UV SENSOR
    uv = ADC(Pin(26))
    vref = ADC(Pin(27))

    print("Test Reading:")
    print(takeReading())
    print("\nConnecting to WLAN:\n")

    #Starting the WLAN work
    wlan = network.WLAN(network.STA_IF)
    wlan.active(True)
    wlan.config(pm=0xa11140) #Regular power mode.
    #Connecting:
    wlan.connect(secret.wifi_SSID, secret.wifi_Pass)

    ##INITIAL CONNECTION DATA:
    timer = 4
    while timer > 0:
        if wlan.status() >= 3 or wlan.status() < 0:
            print("Waiting to Connect...\n")
            timer -= 1
            led.value(1)
            time.sleep(1)

    if wlan.status() != 3:
        print("Connection Error")
        led.value(1)
        print("quitting")
        exit(-1)
    else:
        print("Connected, WLAN Config:")
        print(wlan.ifconfig())
        print("\n")
        led.value(0)

    #At this point we should have a sucsessful WLAN connection, alternativley we restarted.

    #this loop allows for the Re-doing of the entire UDP broadcast cycle
    #to rejoin the data server.
    while True:
        addr = socket.getaddrinfo('0.0.0.0', 80)[0][-1]  # Getting my OWN address info from the router.

        udp_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)  #AF_INET, UDP
        udp_sock.bind(("", 51519))

        binded = False
        tcp_address = ""
        tcp_port = -1

        while not binded:
            try:
                #Searching
                print("UDP SEARCH CYCLE")
                link_data = udp_sock.recv(1024)
                data = link_data.decode()

                print("Recieved A UDP Broadcast\n")

                print(data)
                #Decoding data
                json_msg = json.loads(data)
                if data != "" and json_msg is not None:
                    if json_msg["ID"] == "PicoCast":
                        tcp_address = json_msg["ip"]
                        tcp_port = json_msg["port"]
                        binded = True
                        print("Recieved A PicoCast message || IP:{}, Port:{}, Iteration:{}\n".format(tcp_address, tcp_port, json_msg["iter"]))


            except ValueError as ve:
                #This includes a JSON decoder error but also casts.
                print("Invalid udp broadcast JSON decoding or invalid json formatting!\n\n")
                print(ve)
                udp_sock.close()
                break

            except Exception as e:
                print("ERROR! \n\n")
                print(e)
                udp_sock.close()
                break

        #Now we bind to the server and send data!

        #Close the UDP section
        udp_sock.close()

        #Start working on talking to the server via TCP!
        tcp_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

        try:
            print("Attempting to connect to ({}:{})".format(tcp_address, tcp_port))
            tcp_sock.connect((tcp_address, tcp_port))
            print("Connected to server!")
            tcp_sock.send(encodedResponse("CONN"))
        except Exception as e:
            print("CONN ERROR! \n\n")
            print(e)

        #If here, then we've connected correctly!

        #Just stay active unless there was an exception that pulls us out.
        while(True):
            try:

                raw_data = tcp_sock.recv(1024)
                data = raw_data.decode()

                json_msg = json.loads(data)
                if data != "" and json_msg is not None:
                    print("Message Recevied:")
                    print(json_msg)
                    evaluateResponse(tcp_sock, json_msg)
                    print("\n")

                time.sleep(1)

            except ValueError as ve:
                #This includes a JSON decoder error but also casts.
                print("Invalid udp JSON decoding or formatting!\n\n")
                print(ve)
                break #break out to the UDP section

            except Exception as e:
                print("SERVER ERROR! \n\n")
                print(e)
                udp_sock.close()
                tcp_sock.close()
                break #break out to the UDP section

            except KeyboardInterrupt as kb:
                print("KeyoboardInterrupt Triggered")
                tcp_sock.send(encodedResponse("CLOSE"))
                tcp_sock.close()
                quit()

#This function evaluates a response to send to the sever
def evaluateResponse(sock, msg):
    global all_readings

    if msg["STATUS"] == "ACK": #SIMPLE ACK BACK AND FOURTH
        print("Acked!")
        sock.send(encodedResponse("MES"))
    if msg["STATUS"] == "DATA": #REQUESTING ALL SAVED DATA FROM SESSION
        print("Data Requested!")
        sock.send(encodedResponse("DATA"))
    if msg["STATUS"] == "RST": #REQUESTING ALL SAVED DATA FROM SESSION
        print("Local Reset Requested")
        all_readings = []
        sock.send(encodedResponse("MES"))

#This function encodes the requested response to be sent
def encodedResponse(msg):
    global all_readings

    json_msg = {}
    json_msg["STATUS"] = ""
    json_msg["DATA"] = []

    if msg == "CONN":
        json_msg["STATUS"] = "CONN"
        json_msg["DATA"] = []
    if msg == "DATA":
        json_msg["STATUS"] = "DATA"
        json_msg["DATA"] = all_readings
    if msg == "MES":
        json_msg["STATUS"] = "MES"
        reading = takeReading()
        json_msg["DATA"].append(reading)
    if msg == "CLOSE":
        json_msg["STATUS"] = "CLOSE"
        json_msg["DATA"] = []



    return json.dumps(json_msg).encode()

#This function is from the UV sensor's datasheet
def voltageToIntensity(voltage):
    #transforming the voltage to UV index
    #based on the sensor's linear voltage-UV graph.
    return (voltage-0.99) * (15.0) / (2.8 - 0.99)

#This grabs the time from the RTC
def getTime():
    global rtc
    global initial_time

    t = rtc.datetime_seconds()
    true_time = t - initial_time

    return true_time

#This Takes and entire reading!
def takeReading():
    global background_measure_counter
    global  background_measure_thresh

    json_msg = {}
    json_msg["TIME"] = getTime()
    temp.measure()
    json_msg["TEMP"] = temp.temperature()
    json_msg["HUM"] = temp.humidity()

    UVVoltage = 3.3 * uv.read_u16() / 65536
    REFVoltage = 3.3 * vref.read_u16() / 65536
    frac = 3.3 / REFVoltage * UVVoltage

    json_msg["UV"] = voltageToIntensity(frac)

    #Does not work. VSYS ADC readings disable WiFi for SOME reason.
    #json["BATT"] = 1.32#read_battery_percent()

    background_measure_counter += 1
    if background_measure_counter > background_measure_thresh:
        background_measure_counter = 0
        all_readings.append(json_msg)

    return json_msg

#Will NOT work with NETWORKING. Depreciated.
def read_battery_percent():
    global vsys

    #from LIPO SHIM documentation.
    voltage = vsys.read_u16() * 3 * 3.3 / 65535 #Converting digital resolution to a voltage
    percentage = 100 * ((voltage - 2.8) / (4.2 - 2.8)) #converting 0-1 to 2.8-4.2, the LiPo charge range.
    if percentage > 100:
        percentage = 100.00
    return percentage


#Auto-start
if __name__ == '__main__':
    main()







