# COMP 4300 Final Project:

By Jacob Diaz, for COMP 4300 (Networking) at the University of Manitoba, 2022.

### Topic:

Creating an IoT device that can perform data measurements and work seamlessly over the user's local network. This device is paired with an Android application that allows the user to execute commands as well as take live and background readings.

The purpose of this device is to solve practical problems regarding ambient conditions like temperature, humidity, and UV light affecting a 3D printer's ability to create object properly

This project is summarized and guided within **Report.docx**, please read it to get the full project explanation!

### Tools:

The tools/libs used in this project are:
* [*PyCharm IDE*](https://www.jetbrains.com/pycharm/) - for [MicroPython](https://micropython.org/) Raspberry Pi Pico W development.
* [*Unity 2021*](https://unity.com/releases/2021-lts) - for Android C# development
* [*Newtonsoft.JSON*](https://www.newtonsoft.com/json) - for C# JSON serialization
* [*Shapes*](https://acegikmo.com/shapes/) - A vector graphic library used to improve the look of the mobile app.
    * *NOTE: the Shapes library by Freya Holmér was omitted from this GitHub project to respect copyright*
    
### Project Structure:

**Report.docx** - The entire project report that goes through all details of the project. This describes and guides the project as a whole.

**./App/** - The directory holding the Unity Android mobile app project

**./App/Pico App/Assets/** - Holds all Unity project assets

**./App/Pico App/Assets/NetworkManager.cs** - The core Networking implementation for the app, created by Jacob Diaz

**./App/Pico App/Assets/UIManager.cs** - The manager for the app's UI, created by Jacob Diaz

**./PythonProject/** - The directory holding the Pico MicroPython project.

**./PythonProject/main.py** - The entire Pico networking and sensing implementation

**3DMockup.blend** - The 3D files associated with the final device's enclosure and the Blender file used to render images.

**./Schematics/** - The physical device's schematics and associated prospective PCB layout.

**./Datasheets/** - Holds any relevant datasheets distributed with the hardware used

**./Media/** - All images and/or video created for this project

**./Media/External Media/** - This folder contains media that I do not own or claim any right to, it is cited in the report. All images/video *not* in this folder was created by Jacob Diaz alone.

    
    
### **[Click here for the demo video](https://www.youtube.com/watch?v=cjnJ7qvFyeA)**
    
