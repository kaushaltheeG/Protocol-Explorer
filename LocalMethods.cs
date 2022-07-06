using System;
using CommandMessenger;
using CommandMessenger.Transport.Serial;
using RoboclawClassLib;
using SerialPortLib;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.IO.Ports;
using System.Reflection;
using SerialPort = System.IO.Ports.SerialPort;
using CustomMssgBox;
using OmegaScript.Scripts;

namespace OmegaScript
{

    /// <summary>
    /// Example implementation of a script with plug-in interface. Note that the type 
    /// inherits from MarshalByRefObject - this is necessary to ensure that 
    /// method calls occur within the plug-in AppDomain.
    /// </summary>
    public partial class OmegaScript : MarshalByRefObject, OmegaScriptCommon.IOmegaScript
    {
        public static CommonScriptParameters myCommonParams = new CommonScriptParameters();
        public static string comPortA = myCommonParams.ComPortA;
        public static string comPortB = myCommonParams.ComPortB;
        public static string comPortC = myCommonParams.ComPortC;
        public static string comPortD = myCommonParams.ComPortD;
        public static short engageRPM = 95;
        public static int speed = 200;

        public bool cannulaProb = false;
        public bool aborting = false;
        public bool abortMaintenance = false;

        public string rSource;
        public double disruptVol;
        public int disruptCycles;
        public int incubateCycles;
        public string mySpecies;
        public string runOutput;
        public string disruptionStyle;
        public string tissue;
        public bool predisruptChoice;
        public int incTime;
        public string mixingStyle;
        public int mixingSpeed;
        public int disruptionSpeed;
        public int vertSpeed;
        public string runTemp;
        public string reagentSource;
        public bool tempOffFlag = false;

        public bool thinLine = false;


        public ProtocolParams pParams;
        public TimedMssgBox.frmTimedMssgBox msgBox;
        CustomMssgBox.CustomMssgBox cusMsgBox2 = new CustomMssgBox.CustomMssgBox();
        public panelControlerFFPE panelFFpe = new panelControlerFFPE();
        public int TempTimeOut()
        {
            //throw up timed message box
            //if yes then restart cooling timer
            //if no then change machineState
            //if unclicked then turn off and log
            msgBox = new TimedMssgBox.frmTimedMssgBox("Continue Cooling?", "Instrument Cooling will be turned off in 15 minutes.\nContinue cooling instrument?", 15);
            msgBox.ShowDialog();
            if (msgBox.Result == -1)
            {
                ScriptLog(Severity.Control, "User chose to end cooling.");
                return 0;
            }
            else if (msgBox.Result == 0)
            {
                ScriptLog(Severity.Control, "Instrument cooling turned off due to inactivity.");
                tempOffFlag = true;
                return 0;
            }
            else
            {
                ScriptLog(Severity.Control, "User chose to continue cooling.");
                return 2;
            }
        }

        public void RefreshProtocolParameters()
        {
            //This will deserialize any serialized parameter set sent by the host

            ScriptLog(Severity.Control, "Getting Protocol Parameters...");
            pParams = new ProtocolParams();

            if (SerializedProtocolParameters == "")
                return; //looks like our host screwed up!
            else
                ScriptLog(Severity.Control, SerializedProtocolParameters);

            pParams = pParams.FromJSON(SerializedProtocolParameters);

        }

        public void TranslateParameters()
        {
            int[] ZSpeed = { 5000, 10000, 20000, 30000, 40000 };
            rSource = ((ReagentSource)pParams.ControlParameters.ReagentSource).ToString();
            disruptVol = pParams.ControlParameters.DisruptVol;
            if (pParams.ControlParameters.Xtra == 1)
            {
                disruptVol = 1.0;
            }
            disruptCycles = pParams.ControlParameters.DisruptAmt;
            incubateCycles = pParams.ControlParameters.IncubateAmt;
            if (pParams.ControlParameters.Xtra == 2)
            {
                disruptCycles = 2;
                incubateCycles = 1;
            }
            mySpecies = ((Species)pParams.Species).ToString();//
            runOutput = ((CellType)pParams.CellType).ToString();//
            if (runOutput == "Cells")
            {
                disruptionStyle = ((CellDisruptType)pParams.ControlParameters.DisruptionType).ToString();
            }
            else
            {
                disruptionStyle = ((NucleiDisruptType)pParams.ControlParameters.DisruptionType).ToString();
            }
            if (disruptionStyle == "Nuclei_Dounce" || disruptionStyle == "Cell_Triturate")
            {
                vertSpeed = ZSpeed[pParams.ControlParameters.DisruptionSpeed];
            }
            tissue = ((TissueType)pParams.TissueType).ToString();//
            predisruptChoice = pParams.ControlParameters.MinceAuto;//
            incTime = pParams.ControlParameters.IncubationTime;
            mixingStyle = ((MixingType)pParams.ControlParameters.MixingType).ToString();
            mixingSpeed = 45 + (pParams.ControlParameters.MixingSpeed * 25);
            disruptionSpeed = 45 + (pParams.ControlParameters.DisruptionSpeed * 25);
            runTemp = ((Temperature)pParams.ControlParameters.IncubationTemp).ToString();
            ScriptLog(Severity.Info, "Running Protocol: " + pParams.ProtocolName);
            ScriptLog(Severity.Info, "Species: " + mySpecies + ", Tissue: " + tissue + ", Output: " + runOutput);
            ScriptLog(Severity.Info, "Incubation: Cycles = " + incubateCycles + ", Time = " + incTime + ", Temp = " + runTemp);
            ScriptLog(Severity.Info, "            Mixing Style = " + mixingStyle + ", Speed = " + mixingSpeed);
            ScriptLog(Severity.Info, "Disruption: Cycles = " + disruptCycles + ", Style = " + disruptionStyle + ", Speed = " + disruptionSpeed);
            ScriptLog(Severity.Info, "Disruption Vol = " + disruptVol + ", Predisrupt? = " + predisruptChoice + ", Reagent Source = " + rSource);
            ScriptLog(Severity.Info, "Run Name: " + pParams.RunParameters.RunName + ", User: " + pParams.RunParameters.User + ", Tissue Amt: " + pParams.RunParameters.TissueAmount);
            ScriptLog(Severity.Info, "Run Notes: " + pParams.RunParameters.RunNotes);
        }

        public class Cavro
        {
            //Instrument myins = new Instrument();

            OmegaScript myParentScript;
            //CStor
            public Cavro(OmegaScript myScript)
            {
                myParentScript = myScript;
            }

            private static string ScriptName = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            private static void ScriptLog(Severity mySeverity, string myLogMessage)
            {
                MAS.Framework.Logging.Lib.Logger.Log((MAS.Framework.Logging.Lib.Severity)mySeverity, myLogMessage, ScriptName);
            }

            //Cavro Pump Support
            public CavroPump Device { get; set; }

            public string Connect(List<string> ports)
            {
                bool exitloop = false;
                string cavroport = "Null";
                foreach (string port in ports)
                {
                    if (!exitloop)
                    {
                        try
                        {
                            Setup(port, 1);
                            string firmwarever = GetFirmwareVersion();
                            if (firmwarever.Contains("C3000"))
                            {
                                cavroport = port;
                                exitloop = true;
                            }
                            else
                            {
                                Cleanup();
                            }
                        }
                        catch (Exception e)
                        {
                            //do nothing
                        }
                    }
                }
                return cavroport;
            }

            public bool Setup()
            {
                int numDevices = 2;
                Device = new CavroPump();
                for (int i = 1; i <= numDevices; i++)
                {
                    bool retVal = Device.Connect("COM17", 9600, i.ToString()); //returns true if connection succeeded
                    if (!retVal) return retVal; //failed
                    Device.Reset(false);
                }
                return true;
            }

            public bool Setup(string comPort)
            {
                if (!myParentScript.abortRequested)
                {
                    try
                    {
                        int numDevices = 2;
                        Device = new CavroPump();
                        for (int i = 1; i <= numDevices; i++)
                        {
                            bool retVal = Device.Connect(comPort, 9600, i.ToString()); //returns true if connection succeeded
                            if (!retVal) return retVal; //failed
                            Device.Reset(false);
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }
                return true;
            }

            public string GetFirmwareVersion()
            {
                string reply;
                reply = Device.SendCmdGetResponse("&"); //get firmware version
                int fwStrIndex = reply.IndexOf("/0");
                reply = reply.Substring(fwStrIndex + 3);
                return reply;
            }

            public void InitializeThreeDevices()
            {
                for (int i = 1; i <= 3; i++)
                {
                    SetActiveDevice(i);
                    //Device.SendCmdGetResponse("f1F0j2o1500m60h1L10V700Z200P100R");
                    Device.SendCmdGetResponse("ZR");
                    Device.AwaitPumpReady(10000);
                }
            }

            public void SetActiveDevice(int devAddress)
            {
                Device.SetDeviceAddress(devAddress.ToString());
            }

            public void CleanupCellRun()
            {

                #region Wash Enzyme and Buffer Lines
                NewMoveToValve(12);
                AdjustPumpSpeed(600);
                MoveToWaitDone(3000, 100000);
                NewMoveToValve(11);
                MoveToWaitDone(2000, 100000);
                NewMoveToValve(10);
                MoveToWaitDone(0, 100000);
                #endregion

                #region Clear Enzyme and Buffer Lines
                NewMoveToValve(9);
                MoveToWaitDone(750, 100000);
                NewMoveToValve(6);
                MoveToWaitDone(0, 100000);

                NewMoveToValve(9);
                MoveToWaitDone(3000, 100000);
                NewMoveToValve(11);
                MoveToWaitDone(2000, 100000);
                NewMoveToValve(10);
                MoveToWaitDone(0, 100000);
                #endregion

                //Device.Disconnect();
            }

            public void Cleanup()
            {
                #region Wash Lines

                PrimeWater();
                ClearLines();

                #endregion

                //Device.Disconnect();
            }

            public void Discon()
            {
                Device.Disconnect();
            }

            public void NewMoveToValve(int valveNumber)
            {
                if (!myParentScript.abortRequested)
                {
                    int valve = 0;
                    if (valveNumber <= 6)
                    {
                        valve = valveNumber;
                        SetActiveDevice(1);
                    }
                    else if (valveNumber <= 12)
                    {
                        valve = valveNumber - 6;
                        SetActiveDevice(1);
                        Device.SendCmdGetResponse("I2R");
                        CheckPumpReady(5);
                        //Device.AwaitPumpReady(5000);
                        SetActiveDevice(2);
                    }
                    else if (valveNumber <= 18)
                    {
                        valve = valveNumber - 12;
                        SetActiveDevice(1);
                        Device.SendCmdGetResponse("I2R");
                        CheckPumpReady(5);
                        //Device.AwaitPumpReady(5000);
                        SetActiveDevice(2);
                        Device.SendCmdGetResponse("I3R");
                        CheckPumpReady(5);
                        //Device.AwaitPumpReady(5000);
                        SetActiveDevice(3);
                    }
                    Device.SendCmdGetResponse("I" + valve + "R");
                    CheckPumpReady(5);
                    //Device.AwaitPumpReady(5000);
                    SetActiveDevice(1);

                    if (valveNumber == 11 | valveNumber == 2 && myParentScript.thinLine)
                    {
                        //AdjustPumpSpeed(50);
                    }
                    else
                    {
                        AdjustPumpSpeed(600);
                    }
                }
            }

            public void MoveToWaitDone(int Position, int Wait)
            {
                if (!myParentScript.abortRequested)
                {
                    Device.SendCmdGetResponse("A" + Position + "R");
                    CheckPumpReady(Wait / 1000);
                    //Device.AwaitPumpReady(Wait);
                }
            }

            public bool MoveToWaitDoneErrorCheck(int Position, int Wait)
            {
                bool error = false;
                ScriptLog(Severity.Control, "Error variable = " + error);
                if (!myParentScript.abortRequested)
                {
                    ScriptLog(Severity.Control, "Syringe moving to abs position: " + Position);
                    Device.SendCmdGetResponse("A" + Position + "R");
                    ScriptLog(Severity.Control, "Checking Pump Ready");
                    error = CheckPumpReadywError(Wait / 1000);
                    //CheckPumpReady(Wait / 1000);
                    //Device.AwaitPumpReady(Wait);
                }
                ScriptLog(Severity.Control, "Error variable after command = " + error);
                return error;
            }

            public void AdjustPumpSpeed(int Speed)
            {
                SetActiveDevice(1);
                Device.SendCmdGetResponse("V" + Speed + "R");
                Device.SendCmdGetResponse("v" + Speed + "R");
            }

            public void AdjustPumpPower(int pow)
            {
                SetActiveDevice(1);
                Device.SendCmdGetResponse("m" + pow + "R");
            }

            public void PrimeEnz()
            {
                if (!myParentScript.abortRequested)
                {
                    myParentScript.myInstrument.enzymeLoaded = true;
                    NewMoveToValve(11);
                    //AdjustPumpSpeed(600);
                    MoveToWaitDone(480, 100000);
                    NewMoveToValve(6);
                    MoveToWaitDone(0, 100000);
                }
            }

            public void PrimeBuffer()
            {
                if (!myParentScript.abortRequested)
                {
                    NewMoveToValve(10);
                    AdjustPumpSpeed(600);
                    MoveToWaitDone(480, 100000);
                    NewMoveToValve(6);
                    MoveToWaitDone(0, 100000);
                }
            }

            public void PrimeNucIso()
            {
                if (!myParentScript.abortRequested)
                {
                    NewMoveToValve(7);
                    AdjustPumpSpeed(600);
                    MoveToWaitDone(540, 100000);
                    NewMoveToValve(6);
                    MoveToWaitDone(0, 100000);
                }
            }

            public void PrimeNucStor()
            {
                if (!myParentScript.abortRequested)
                {
                    NewMoveToValve(8);
                    AdjustPumpSpeed(600);
                    MoveToWaitDone(540, 100000);
                    NewMoveToValve(6);
                    MoveToWaitDone(0, 100000);
                }
            }

            public void Prime(int valvePos)
            {
                ///var FFPE = new panelControlerFFPE();


                NewMoveToValve(valvePos);
                AdjustPumpSpeed(600);
                MoveToWaitDone(480, 100000);
                NewMoveToValve(6);
                MoveToWaitDone(0, 100000);

            }
            #region Delivery & Strain for Panel
            public void halfDeliver()
            {
                var FFPE = new panelControlerFFPE();
                short pumpSpd = Convert.ToInt16(FFPE.setDeliverySpd);

                if (!myParentScript.abortRequested)
                {
                    ScriptLog(Severity.Info, "Delivering Half Amount");
                    ScriptLog(Severity.Control, "Delivering Half Amount");
                    NewMoveToValve(FFPE.valvePos); //NewMoveToValve(11) or (12)
                    AdjustPumpSpeed(pumpSpd);
                    MoveToWaitDone(300, 100000); //prime 0.5ml 
                    NewMoveToValve(9); //prime air
                    MoveToWaitDone(600, 100000); //1ml
                    
                    NewMoveToValve(3); //push to disrupt
                    AdjustPumpSpeed(pumpSpd);
                    MoveToWaitDone(0, 100000);

                    NewMoveToValve(9); 
                    MoveToWaitDone(1200, 100000);
                    NewMoveToValve(3);
                    AdjustPumpSpeed(pumpSpd);
                    MoveToWaitDone(0, 100000);
                }
            }

            public void oneDeliver()
            {
                var FFPE = new panelControlerFFPE();
                short pumpSpd = Convert.ToInt16(FFPE.setDeliverySpd);

                if (!myParentScript.abortRequested)
                {
                    ScriptLog(Severity.Info, "Delivering Half Amount");
                    ScriptLog(Severity.Control, "Delivering Half Amount");
                    NewMoveToValve(FFPE.valvePos); //NewMoveToValve(11) or (12)
                    AdjustPumpSpeed(pumpSpd);
                    MoveToWaitDone(600, 100000); //prime 1ml 
                    NewMoveToValve(9); //prime air
                    MoveToWaitDone(1200, 100000); //2ml

                    NewMoveToValve(3); //push to disrupt
                    AdjustPumpSpeed(pumpSpd);
                    MoveToWaitDone(0, 100000);

                    NewMoveToValve(9);
                    MoveToWaitDone(1200, 100000);
                    NewMoveToValve(3);
                    AdjustPumpSpeed(pumpSpd);
                    MoveToWaitDone(0, 100000);
                }
            }

            public void twoDeliver()
            {
                var FFPE = new panelControlerFFPE();
                short pumpSpd = Convert.ToInt16(FFPE.setDeliverySpd);

                if (!myParentScript.abortRequested)
                {
                    ScriptLog(Severity.Info, "Delivering Half Amount");
                    ScriptLog(Severity.Control, "Delivering Half Amount");
                    NewMoveToValve(FFPE.valvePos); //NewMoveToValve(11) or (12)
                    AdjustPumpSpeed(pumpSpd);
                    MoveToWaitDone(1200, 100000); //prime 2ml 
                    NewMoveToValve(9); //prime air
                    MoveToWaitDone(1800, 100000); //3ml

                    NewMoveToValve(3); //push to disrupt
                    AdjustPumpSpeed(pumpSpd);
                    MoveToWaitDone(0, 100000);

                    NewMoveToValve(9);
                    MoveToWaitDone(1200, 100000);
                    NewMoveToValve(3);
                    AdjustPumpSpeed(pumpSpd);
                    MoveToWaitDone(0, 100000);
                }
            }

            public void flexDeliver( int solutionDeliver, int airPrime, int valvePos, int pumpSpd)
            {
                //var FFPE = new panelControlerFFPE();
                //short pumpSpd = Convert.ToInt16(FFPE.setDeliverySpd);

                if (!myParentScript.abortRequested)
                {
                    //ScriptLog(Severity.Info, "Delivering Half Amount");
                    //ScriptLog(Severity.Control, "Delivering Half Amount");
                    NewMoveToValve(2);
                    NewMoveToValve(valvePos); //NewMoveToValve(11) or (12)
                    AdjustPumpSpeed(pumpSpd); //200); 
                    MoveToWaitDone(solutionDeliver, 100000); //prime solution
                    NewMoveToValve(9); //prime air
                    MoveToWaitDone(airPrime, 100000); //3ml
                    

                    NewMoveToValve(3); //push to disrupt
                    AdjustPumpSpeed(pumpSpd);
                    MoveToWaitDone(0, 100000);

                    NewMoveToValve(9);
                    MoveToWaitDone(1000, 100000);
                    NewMoveToValve(3);
                    AdjustPumpSpeed(pumpSpd); //ffpePanel.whichCatch
                    MoveToWaitDone(0, 100000);
                }
            }

            public void flexStrain(int strainSteps, int speed)
            {
                ScriptLog(Severity.Control, $"|--- Pump Speed set to: {speed}" );
                //ScriptLog(Severity.Control, "....Strain Steps set to: " + strainSteps);
                NewMoveToValve(4); //valve moved to pull from cartridge through filter
                AdjustPumpSpeed(speed); //300); //pull speed through filter
                MoveToWaitDone(strainSteps, 300000); //steps required to strain depending on user's input
                NewMoveToValve(6); //valve moved to vent to reset syringe pump
                AdjustPumpSpeed(600);
                MoveToWaitDone(0, 100000); //pump pushes full stroke
            }

            #endregion

            public void PrimeWater()
            {
                if (!myParentScript.abortRequested)
                {
                    NewMoveToValve(12);
                    AdjustPumpSpeed(600);
                    MoveToWaitDone(750, 100000);
                    NewMoveToValve(6);
                    MoveToWaitDone(0, 100000);
                }
            }

            public void ClearLines()
            {
                if (!myParentScript.abortRequested)
                {
                    NewMoveToValve(9);
                    MoveToWaitDone(750, 100000);
                    NewMoveToValve(6);
                    MoveToWaitDone(0, 100000);
                }
            }

            public void PrimeBufferandClearLines()
            {
                if (!myParentScript.abortRequested)
                {
                    if (myParentScript.runOutput == "Nuclei")
                    {
                        ScriptLog(Severity.Info, "Priming NSR from Single Shot");
                        ScriptLog(Severity.Control, "Priming NSR from Single Shot");
                    }
                    else
                    {
                        ScriptLog(Severity.Info, "Priming Buffer");
                        ScriptLog(Severity.Control, "Priming Buffer");
                    }

                    PrimeBuffer();

                    ClearLines();

                    ScriptLog(Severity.Control, "Second Prime Finished.");
                }
            }

            public void PrimeNucStorandClearLines()
            {
                if (!myParentScript.abortRequested)
                {
                    ScriptLog(Severity.Info, "Priming NSR");
                    ScriptLog(Severity.Control, "Priming NSR");

                    PrimeNucStor();

                    ClearLines();

                    ScriptLog(Severity.Control, "Second Prime Finished.");
                }
            }

            public void PrimeEnzandClearLines()
            {
                if (!myParentScript.abortRequested)
                {
                    if (myParentScript.runOutput == "Nuclei")
                    {
                        ScriptLog(Severity.Info, "Priming Nuc Iso from Single Shot");
                        ScriptLog(Severity.Control, "Priming Nuc Iso from Single Shot");
                    }
                    else
                    {
                        ScriptLog(Severity.Info, "Priming Enzyme");
                        ScriptLog(Severity.Control, "Priming Enzyme");
                    }

                    PrimeEnz();

                    PrimeWater();

                    ClearLines();

                    ScriptLog(Severity.Control, "First Prime Finished");
                }

            }

            public void PrimeNucIsoandClearLines()
            {
                if (!myParentScript.abortRequested)
                {
                    ScriptLog(Severity.Info, "Priming NIR");
                    ScriptLog(Severity.Control, "Priming NIR");

                    PrimeNucIso();

                    PrimeWater();

                    ClearLines();

                    ScriptLog(Severity.Control, "First Prime Finished");
                }
            }

            public void DeliverEnz()
            {
                if (!myParentScript.abortRequested)
                {
                    ScriptLog(Severity.Info, "Delivering Enzyme");
                    ScriptLog(Severity.Control, "Delivering Enzyme");
                    NewMoveToValve(11);
                    MoveToWaitDone(1200, 100000);
                    NewMoveToValve(9);
                    MoveToWaitDone(1950, 100000);
                    NewMoveToValve(3);
                    AdjustPumpSpeed(200);
                    MoveToWaitDone(0, 100000);

                    NewMoveToValve(9);
                    MoveToWaitDone(1200, 100000);
                    NewMoveToValve(3);
                    AdjustPumpSpeed(200);
                    MoveToWaitDone(0, 100000);
                }
            }
            public string CheckPumpError()
            {
                string reply;
                reply = Device.SendCmdGetResponse("?10");
                int fwStrIndex = reply.IndexOf("/0");
                reply = reply.Substring(fwStrIndex + 3);
                reply = reply.TrimEnd('\r', '\n');
                //int reply1 = Convert.ToInt32(reply);
                return reply;
                //reply is response message sent to check syringe pump delivered solution
            }

            public void PushToCannulas(string cannula, int position, int speed)
            {
                int cann;
                bool cannReturn = false;
                if (cannula == "disruption")
                {
                    cann = 3;
                }
                else
                {
                    cann = 5;
                }
                #region Push to Cannulas
                NewMoveToValve(cann);
                AdjustPumpSpeed(speed);

                ///////////////////////////////////added adjusting pump power
                AdjustPumpPower(18);
                //////////////////////////////////////////////////////////////
                ScriptLog(Severity.Control, "Delivering while checking for error...");
                cannReturn = MoveToWaitDoneErrorCheck(position, 100000);
                //MoveToWaitDone(position, 100000);

                bool cannulaBad = false;
                
                if (position == 0)
                {
                    cannulaBad = cannReturn;
                }

                if (cannulaBad)
                {
                    //if pump fails...

                    myParentScript.cannulaProb = true;

                    //throw error to log
                    ScriptLog(Severity.Info, "Delivery Failed. Check Cartridge.");
                    ScriptLog(Severity.Control, "Delivery Failed. Check Cartridge.");
                    //FIXTHIS
                    myParentScript.cusMsgBox2.Show("Delivery Failed. Check Cartridge.", "Delivery Failed", MessageBoxButtons.OK);
                    //MessageBox.Show("Delivery Failed. Check Cartridge.", "Delivery Failed", MessageBoxButtons.OK);
                    //initialize syringe
                    Initialize(2);

                    ////pull from line
                    //AdjustPumpPower(75);
                    //NewMoveToValve(cann);
                    //AdjustPumpSpeed(600);
                    //MoveToWaitDone(3000, 100000);
                    ////push to waste
                    //NewMoveToValve(6);
                    //MoveToWaitDone(0, 100000);
                    if (!myParentScript.aborting)
                    {
                        ScriptLog(Severity.Info, "Aborting Run...");
                        ScriptLog(Severity.Control, "Aborting Run...");
                        //signal abort
                        myParentScript.abortRequested = true;
                    }
                }
                else
                {
                    //if everything is good, change power back
                    AdjustPumpPower(75);
                }
                //////////////////////////////////////////////////////////////////
                #endregion
            }

            public void DeliverEnz(int amt)
            {
                if (!myParentScript.abortRequested)
                {
                    if (myParentScript.runOutput != "Nuclei")
                    {
                        ScriptLog(Severity.Info, "Delivering Enzyme");
                        ScriptLog(Severity.Control, "Delivering Enzyme: " + amt + " Steps");
                    }
                    else
                    {
                        ScriptLog(Severity.Info, "Delivering NIR from Single Shot");
                        ScriptLog(Severity.Control, "Delivering NIR: " + amt + " Steps");
                    }
                    NewMoveToValve(11);
                    MoveToWaitDone(amt, 100000);
                    NewMoveToValve(9);
                    MoveToWaitDone(amt + 750, 100000);
                    

                    PushToCannulas("disruption", 0, 200);

                    //NewMoveToValve(3);
                    //AdjustPumpSpeed(200);
                    //MoveToWaitDone(0, 100000);

                    NewMoveToValve(9);
                    MoveToWaitDone(1200, 100000);
                    NewMoveToValve(3);
                    AdjustPumpSpeed(200);
                    MoveToWaitDone(0, 100000);
                }
            }

            public void DeliverBuffer()
            {
                if (!myParentScript.abortRequested)
                {
                    if (myParentScript.runOutput != "Nuclei")
                    {
                        ScriptLog(Severity.Info, "Delivering Buffer");
                        ScriptLog(Severity.Control, "Delivering Buffer");
                    }
                    else
                    {
                        ScriptLog(Severity.Info, "Delivering NSR from Single Shot");
                        ScriptLog(Severity.Control, "Delivering NSR");
                    }
                    NewMoveToValve(10);
                    MoveToWaitDone(1200, 100000);
                    NewMoveToValve(9);
                    MoveToWaitDone(1950, 100000);

                    PushToCannulas("disruption", 0, 200);

                    //NewMoveToValve(3);
                    ////AdjustPumpSpeed(200);
                    //MoveToWaitDone(0, 100000);

                    NewMoveToValve(9);
                    MoveToWaitDone(1200, 100000);
                    NewMoveToValve(3);
                    AdjustPumpSpeed(200);
                    MoveToWaitDone(0, 100000);
                }
            }
            
            public void DeliverNucIso()
            {
                if (!myParentScript.abortRequested)
                {
                    ScriptLog(Severity.Info, "Delivering NIR");
                    ScriptLog(Severity.Control, "Delivering NIR");
                    NewMoveToValve(7);
                    MoveToWaitDone(1200, 100000);
                    NewMoveToValve(9);
                    MoveToWaitDone(1950, 100000);

                    PushToCannulas("disruption", 0, 200);

                    //NewMoveToValve(3);
                    //AdjustPumpSpeed(200);
                    //MoveToWaitDone(0, 100000);

                    NewMoveToValve(9);
                    MoveToWaitDone(1200, 100000);
                    NewMoveToValve(3);
                    AdjustPumpSpeed(200);
                    MoveToWaitDone(0, 100000);
                }
            }

            public void DeliverNucIso(int amt)
            {
                if (!myParentScript.abortRequested)
                {
                    ScriptLog(Severity.Info, "Delivering NIR");
                    ScriptLog(Severity.Control, "Delivering NIR: " + amt + " Steps");
                    NewMoveToValve(7);
                    MoveToWaitDone(amt, 100000);
                    NewMoveToValve(9);
                    MoveToWaitDone(amt + 750, 100000);

                    PushToCannulas("disruption", 0, 200);

                    //NewMoveToValve(3);
                    //AdjustPumpSpeed(200);
                    //MoveToWaitDone(0, 100000);

                    NewMoveToValve(9);
                    MoveToWaitDone(1200, 100000);
                    NewMoveToValve(3);
                    AdjustPumpSpeed(200);
                    MoveToWaitDone(0, 100000);
                }
            }

            public void DeliverNucStor()
            {
                if (!myParentScript.abortRequested)
                {
                    ScriptLog(Severity.Info, "Delivering NSR");
                    ScriptLog(Severity.Control, "Delivering NSR");
                    NewMoveToValve(8);
                    MoveToWaitDone(1200, 100000);
                    NewMoveToValve(9);
                    MoveToWaitDone(1950, 100000);

                    PushToCannulas("disruption", 0, 600);

                    //NewMoveToValve(3);
                    ////AdjustPumpSpeed(200);
                    //MoveToWaitDone(0, 100000);

                    NewMoveToValve(9);
                    MoveToWaitDone(1200, 100000);
                    NewMoveToValve(3);
                    AdjustPumpSpeed(200);
                    MoveToWaitDone(0, 100000);
                }
            }

            public void FlushCannulas()
            {
                NewMoveToValve(9);
                MoveToWaitDone(1200, 100000);

                PushToCannulas("disruption", 0, 600);

                //NewMoveToValve(3);
                //MoveToWaitDone(0, 100000);
            }

            public void StrainSample(int pullSpeed)
            {
                NewMoveToValve(4); //valve moved to pull from cartridge through filter
                AdjustPumpSpeed(pullSpeed); //pull speed through filter
                MoveToWaitDone(3000, 300000); //pump pulls full stroke
                NewMoveToValve(6); //valve moved to vent to reset syringe pump
                AdjustPumpSpeed(600);
                MoveToWaitDone(0, 100000); //pump pushes full stroke
            }



            public void CheckPumpReady(int timeoutSeconds)
            {
                for (int i = 0; i < timeoutSeconds && !myParentScript.abortRequested && !Device.AwaitPumpReady(1000); i++)
                {
                    //do nothing
                }
                //if (myParentScript.abortRequested)
                //{
                //    Device.SendCmdGetResponse("TR");
                //}
            }

            public bool CheckPumpReadywError(int timeoutSeconds)
            {
                ScriptLog(Severity.Control, "#######");
                bool isReady = false;
                int error = 0;
                bool fluidicError = false;
                ScriptLog(Severity.Control, "Fluidic Error variable before loop = " + fluidicError);
                for (int i = 0; i < timeoutSeconds && !myParentScript.abortRequested; i++)
                {
                    isReady = Device.AwaitPumpReady(1000, ref error);
                    //isReady = Device.AwaitPumpReady(1000);
                    if (error > 0)
                    {
                        fluidicError = true;
                        ScriptLog(Severity.Control, "ERROR: " + error);
                        break;
                    }
                    else if (isReady)
                    {
                        ScriptLog(Severity.Control, "Pump move successful!");
                        break;
                    }
                    ScriptLog(Severity.Control, "Loop number " + (i+1) + " of " + timeoutSeconds);
                    //do nothing
                }
                //if (myParentScript.abortRequested)
                //{
                //    Device.SendCmdGetResponse("TR");
                //}
                return fluidicError;
            }

            public void PrimeDecon()
            {
                if (!myParentScript.abortRequested)
                {
                    NewMoveToValve(11);
                    MoveToWaitDone(750, 100000);
                    NewMoveToValve(10);
                    MoveToWaitDone(1500, 100000);
                    NewMoveToValve(6);
                    MoveToWaitDone(0, 100000);
                }
            }

            public void BlowCannulas()
            {
                NewMoveToValve(9);
                MoveToWaitDone(3000, 100000);
                //NewMoveToValve(1);
                //MoveToWaitDone(2000, 100000);

                PushToCannulas("disruption", 1500, 600);
                PushToCannulas("trap", 0, 600);

                //NewMoveToValve(3);
                //MoveToWaitDone(1500, 100000);
                //NewMoveToValve(5);
                //MoveToWaitDone(0, 100000);
            }

            public void RinseCannulas()
            {
                NewMoveToValve(12);
                MoveToWaitDone(1200, 100000);
                //NewMoveToValve(1);
                //MoveToWaitDone(2000, 100000);

                //NewMoveToValve(3);
                //AdjustPumpPower(18);
                //MoveToWaitDone(600, 100000);

                PushToCannulas("disruption", 600, 600);
                PushToCannulas("trap", 0, 600);


                //NewMoveToValve(5);
                //MoveToWaitDone(0, 100000);
            }

            public void CleanCannulas()
            {
                NewMoveToValve(10);
                MoveToWaitDone(1500, 100000);
                NewMoveToValve(11);
                MoveToWaitDone(3000, 100000);
                //NewMoveToValve(1);
                //MoveToWaitDone(2000, 100000);

                PushToCannulas("disruption", 2400, 600);
                PushToCannulas("trap", 0, 600);

                //NewMoveToValve(3);
                //MoveToWaitDone(2400, 100000);
                //NewMoveToValve(5);
                //MoveToWaitDone(0, 100000);
            }

            public void BlowEnzBuffer()
            {
                NewMoveToValve(9);
                MoveToWaitDone(3000, 100000);
                NewMoveToValve(11);
                MoveToWaitDone(2000, 100000);
                NewMoveToValve(10);
                MoveToWaitDone(0, 100000);
            }

            public void RinseEnzBuffer()
            {
                NewMoveToValve(12);
                AdjustPumpSpeed(600);
                MoveToWaitDone(3000, 100000);
                NewMoveToValve(11);
                MoveToWaitDone(1500, 100000);
                NewMoveToValve(10);
                MoveToWaitDone(0, 100000);
            }

            public void CleanEnzBuffer()
            {
                NewMoveToValve(11);
                //AdjustPumpSpeed(600);
                MoveToWaitDone(3000, 100000);
                NewMoveToValve(6);
                MoveToWaitDone(0, 100000);
                NewMoveToValve(10);
                MoveToWaitDone(3000, 100000);
                NewMoveToValve(6);
                MoveToWaitDone(0, 100000);
            }

            public void BlowNIRNSR()
            {
                NewMoveToValve(9);
                MoveToWaitDone(3000, 100000);
                NewMoveToValve(7);
                MoveToWaitDone(2000, 100000);
                NewMoveToValve(8);
                MoveToWaitDone(0, 100000);
            }

            public void RinseNIRNSR()
            {
                NewMoveToValve(12);
                AdjustPumpSpeed(600);
                MoveToWaitDone(3000, 100000);
                NewMoveToValve(7);
                MoveToWaitDone(1500, 100000);
                NewMoveToValve(8);
                MoveToWaitDone(0, 100000);
            }

            public void CleanNIRNSR()
            {
                NewMoveToValve(10);
                AdjustPumpSpeed(600);
                MoveToWaitDone(1500, 100000);
                NewMoveToValve(11);
                AdjustPumpSpeed(600);
                MoveToWaitDone(3000, 100000);
                NewMoveToValve(7);
                MoveToWaitDone(1500, 100000);
                NewMoveToValve(8);
                MoveToWaitDone(0, 100000);
            }

            public bool CheckLine(int valveLine)
            {
                NewMoveToValve(9);
                MoveToWaitDone(3000, 100000);
                NewMoveToValve(valveLine);
                MoveToWaitDone(0, 100000);
                return CheckError1();
            }

            public bool CheckError1()
            {
                NewMoveToValve(9);
                SetActiveDevice(1);
                AdjustPumpSpeed(25);
                DateTime startTime = DateTime.UtcNow;
                MoveToWaitDone(100, 100000);
                DateTime endTime = DateTime.UtcNow;
                AdjustPumpSpeed(600);
                NewMoveToValve(6);
                MoveToWaitDone(0, 100000);
                if (endTime - startTime > TimeSpan.FromSeconds(2))
                {
                    return true;
                }
                else
                    return false;
            }

            #region Not Using Right Now

            public bool OldSetup(string myComPort, int devAddress)
            {
                Device = new CavroPump();
                bool retVal = Device.Connect(myComPort, 9600, devAddress.ToString()); //returns true if connection succeeded
                if (!retVal) return retVal; //failed
                Device.Reset(false);
                return true;
            }

            public bool Setup(string myComPort, int numDevices)
            {
                Device = new CavroPump();
                for (int i = 1; i <= numDevices; i++)
                {
                    bool retVal = Device.Connect(myComPort, 9600, i.ToString()); //returns true if connection succeeded
                    if (!retVal) return retVal; //failed
                    Device.Reset(false);
                }
                return true;
            }

            public void Initialize()
            {
                //Device.SendCmdGetResponse("f1F0j2o1500m60h1L10V700Z200P100R");
                Device.SendCmdGetResponse("ZR");
                Device.AwaitPumpReady(10000);
            }

            public void Initialize(int amountOfDevices)
            {
                if (!myParentScript.abortRequested)
                {
                    for (int i = 1; i <= amountOfDevices; i++)
                    {
                        SetActiveDevice(i);
                        //Device.SendCmdGetResponse("f1F0j2o1500m60h1L10V700Z200P100R");
                        Device.SendCmdGetResponse("ZR");
                        CheckPumpReady(10);
                        //Device.AwaitPumpReady(10000);
                    }
                }
            }

            public void MoveTo(int Position)
            {
                Device.SendCmdGetResponse("A" + Position + "R");
            }

            public void MoveDown(int Steps)
            {
                Device.SendCmdGetResponse("P" + Steps + "R");
            }

            public void MoveUp(int Steps)
            {
                Device.SendCmdGetResponse("D" + Steps + "R");
            }

            public void MoveDownIncrements(int steps, int cycles, int pause)
            {
                int stepincrement = (steps / cycles); //each stepincrement is 150
                Device.SendCmdGetResponse("gP" + stepincrement + "M" + pause + "G" + cycles + "R");
            }

            public void MoveDownWaitDone(int Steps, int Wait)
            {
                Device.SendCmdGetResponse("P" + Steps + "R");
                Device.AwaitPumpReady(Wait);
            }

            public void MoveUpWaitDone(int Steps, int Wait)
            {
                Device.SendCmdGetResponse("D" + Steps + "R");
                Device.AwaitPumpReady(Wait);
            }

            public string GetPosition()
            {
                string reply;
                reply = Device.SendCmdGetResponse("?");
                //int reply1 = Convert.ToInt32(reply);
                return reply;
            }
            
            public string GetPosition2()
            {
                string reply;
                reply = Device.SendCmdGetResponse("?");
                int fwStrIndex = reply.IndexOf("/0");
                reply = reply.Substring(fwStrIndex + 3);
                reply = reply.TrimEnd('\r', '\n');
                return reply;
            }

            public void MovetoValve(int ValveNumber)
            {
                //int ValveNumber2 = ValveNumber - 1;
                Device.SendCmdGetResponse("I" + ValveNumber + "R");
                Device.AwaitPumpReady(5000);
            }

            #endregion

        }

        public class DCmotor
        {
            //Instrument myins = new Instrument();

            OmegaScript myParentScript;
            //CStor
            public DCmotor(OmegaScript myScript)
            {
                myParentScript = myScript;
            }

            //RoboClaw Support:
            //------------------------------------------------------------------------------------------------
            Roboclaw roboClaw;

            public string foldername = @"C:\ProgramData\S2_Logs\EncoderLogs\";
            public string filesuffix = @"_" + DateTime.Now.ToString("yyyy_MM_dd_HHmmss") + "_EncoderLog.csv";
            //public string filename = @"C:\LabScript\EncoderLogs\" + DateTime.Now.ToString("yyyy_MM_dd_HHmmss") + "_EncoderLog.csv";

            private static string ScriptName = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            private static void ScriptLog(Severity mySeverity, string myLogMessage)
            {
                MAS.Framework.Logging.Lib.Logger.Log((MAS.Framework.Logging.Lib.Severity)mySeverity, myLogMessage, ScriptName);
            }

            int encPosStart;
            int encPosEnd;
            string motorDir;

            public string Connect(List<string> ports)
            {
                bool exitloop = false;
                string roboclawport = "Null";
                foreach (string port in ports)
                {
                    //ScriptLog(Severity.Info, "Testing Port: " + port);
                    if (!exitloop)
                    {
                        try
                        {
                            Setup(port);
                            //ScriptLog(Severity.Info, "Setup Worked? " + Setup(port));
                            //ScriptLog(Severity.Info, "Firmware reading: " + GetFirmwareVersion());
                            if (GetFirmwareVersion().Contains("USB Roboclaw"))
                            {
                                roboclawport = port;
                                exitloop = true;
                            }
                            else
                            {
                                Cleanup();
                            }
                        }
                        catch (Exception e)
                        {
                            //do nothing
                        }
                    }
                }
                return roboclawport;
            }

            public bool Setup(string myComPort)
            {
                //string roboClawModel = "";
                //roboClaw = new Roboclaw();
                //Connect:
                if ((roboClaw == null || !roboClaw.IsOpen()) && !myParentScript.abortRequested)
                {
                    try
                    {
                        roboClaw = new Roboclaw(myComPort, 115200, 128); // Open the interface to the RoboClaw
                        roboClaw.Open();
                        //roboClaw.Open(myComPort, ref roboClawModel, 128, 38400); // Open the interface to the RoboClaw
                        //labelRoboClawModel.Text = roboClawModel; // Display the RoboClaw device model number
                        roboClaw.ResetEncoders();
                        if (GetFirmwareVersion().Contains("USB Roboclaw"))
                        {
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        //MessageBox.Show("Com Port Operation Problem: " + ex.Message, "Com Port Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        ScriptLog(Severity.Control, ex.Message);
                        return false;
                        //throw;
                    }
                }
                return false;
            }

            public void SetMotorRPM(short RPM, string direction)
            {
                short directionnum = 1;
                if (roboClaw.IsOpen() && !myParentScript.abortRequested)
                {
                    motorDir = direction;
                    encPosStart = GetPosition();
                    if (direction == "forward")
                        directionnum = 1;
                    else
                        directionnum = -1;
                    uint accel = 500000;
                    short duty = (short)(directionnum * ((RPM * 55) + 349));
                    roboClaw.M1DutyAccel(duty, accel);
                }
            }

            public void RunConstSpeed(int speed)
            {
                if (roboClaw.IsOpen() && !myParentScript.abortRequested)
                {
                    if (speed < 0)
                    {
                        motorDir = "reverse";
                    }
                    else
                    {
                        motorDir = "forward";
                    }
                    encPosStart = GetPosition();
                    roboClaw.M1Speed(speed);
                }
            }

            public void MotorStop()
            {
                if (roboClaw.IsOpen() && !myParentScript.abortRequested)
                {
                    roboClaw.ST_M1Forward(0); // Stop the motor
                    encPosEnd = GetPosition();
                    SaveDCData();
                }
            }

            public List<string> Rotate(int stepnum, int time, short RPM, bool bidirectional)
            {
                int n = 0;
                List<string> encoderlist = new List<string>();
                int encpos1;
                int encpos2;
                for (int i = 0; i < stepnum && !myParentScript.abortRequested; i++)
                {
                    encpos1 = GetPosition();
                    SetMotorRPM(RPM, "forward");
                    Sleep(time);
                    MotorStop();
                    encpos2 = GetPosition();
                    if (n == 0)
                    {
                        encoderlist.Add($",Forward, {(encpos2 - encpos1)}");
                        n++;
                    }
                    else
                    {
                        encoderlist.Add($"\n,,,Forward, {(encpos2 - encpos1)}");
                    }

                    if (bidirectional && !myParentScript.abortRequested)
                    {
                        SetMotorRPM(RPM, "reverse");
                        Sleep(time);
                        MotorStop();
                        encpos1 = GetPosition();
                        encoderlist.Add($"\n,,,Reverse, {(encpos2 - encpos1)}");
                    }
                }
                return encoderlist;

            }

            public List<string> RotateConstVel(int stepnum, int time, short RPM, bool bidirectional)
            {
                int n = 0;
                List<string> encoderlist = new List<string>();
                int encpos1;
                int encpos2;
                for (int i = 0; i < stepnum && !myParentScript.abortRequested; i++)
                {
                    encpos1 = GetPosition();
                    RunConstSpeed(RPM * 20);
                    Sleep(time);
                    MotorStop();
                    encpos2 = GetPosition();
                    if (n == 0)
                    {
                        encoderlist.Add($",Forward, {(encpos2 - encpos1)}");
                        n++;
                    }
                    else
                    {
                        encoderlist.Add($"\n,,,Forward, {(encpos2 - encpos1)}");
                    }
                    if (bidirectional && !myParentScript.abortRequested)
                    {
                        RunConstSpeed(RPM * -20);
                        Sleep(time);
                        MotorStop();
                        encpos1 = GetPosition();
                        encoderlist.Add($"\n,,,Reverse, {(encpos2 - encpos1)}");
                    }
                }
                return encoderlist;

            }

            public List<string> RotateOnce(int time, short RPM, bool bidirectional)
            {
                List<string> encoderlist = new List<string>();
                int encpos1;
                int encpos2;
                for (int i = 0; i < 1 && !myParentScript.abortRequested; i++)
                {
                    encpos1 = GetPosition();
                    SetMotorRPM(RPM, "forward");
                    Sleep(time);
                    //Thread.Sleep(time);
                    MotorStop();
                    encpos2 = GetPosition();
                    encoderlist.Add($",Forward, {(encpos2 - encpos1)}");

                    if (bidirectional && !myParentScript.abortRequested)
                    {
                        SetMotorRPM(RPM,"reverse");
                        Sleep(time);
                        //Thread.Sleep(time);
                        MotorStop();
                        encpos1 = GetPosition();
                        encoderlist.Add($"\n,,,Reverse, {(encpos2 - encpos1)}");
                    }
                }
                return encoderlist;

            }

            public List<string> RotateOnceConstVel(int time, short RPM, bool bidirectional)
            {
                List<string> encoderlist = new List<string>();
                int encpos1;
                int encpos2;
                for (int i = 0; i < 1; i++)
                {
                    encpos1 = GetPosition();
                    RunConstSpeed(RPM * 20);
                    Thread.Sleep(time);
                    MotorStop();
                    encpos2 = GetPosition();
                    encoderlist.Add($",Forward, {(encpos2 - encpos1)}");
                    if (bidirectional)
                    {
                        RunConstSpeed(RPM * -20);
                        Thread.Sleep(time);
                        MotorStop();
                        encpos1 = GetPosition();
                        encoderlist.Add($"\n,,,Reverse, {(encpos2 - encpos1)}");
                    }
                }
                return encoderlist;

            }

            //used during regular run
            public int GetPosition()
            {
                int encoder = 999999999;
                if (!myParentScript.abortRequested)
                {
                    byte status = 0;
                    try
                    {
                        roboClaw.GetM1Encoder(ref encoder, ref status);
                    }
                    catch (Exception e)
                    {
                        //Do nothing
                    }
                }
                return encoder;
            }

            public void SaveDCData()
            {
                if (myParentScript.myInstrument.newStep)
                {
                    myParentScript.myInstrument.motorData.Add($",{motorDir},{(encPosEnd - encPosStart)}");
                    myParentScript.myInstrument.newStep = false;
                }
                else
                {
                    myParentScript.myInstrument.motorData.Add($"\n,,,,{motorDir},{(encPosEnd - encPosStart)}");
                }
            }

            public void Sleep(int milliSeconds)
            {
                bool quit = false;
                for (int z = 0; z < (milliSeconds / 250) && !quit; z++)
                {
                    Thread.Sleep(250);
                    if (myParentScript.abortRequested)
                    {
                        quit = true;
                    }
                }
            }

            //used in diagnostic routine
            public int ReadEncoderPosition()
            {
                int encoder = 0;
                byte status = 0;
                roboClaw.GetM1Encoder(ref encoder, ref status);
                return encoder;
            }

            public string GetFirmwareVersion() //KC: not sure what the address should be
            {
                string version = "";
                byte add = 0x80;
                roboClaw.GetVersion(add, ref version);
                return version;
            }

            #region Unused

            public void Cleanup()
            {
                if (roboClaw.IsOpen())
                {
                    roboClaw.ST_M1Forward(0); // Stop the motor
                }
                roboClaw.Close();
            }

            public void SetMotorRPM(short RPM)
            {
                if (roboClaw.IsOpen() && !myParentScript.abortRequested)
                {
                    uint accel = 500000;
                    short duty = (short)((RPM * 55) + 349);
                    roboClaw.M1DutyAccel(duty, accel);
                }
            }

            public bool Setup()
            {
                string roboClawModel = "";
                //Connect:
                if (roboClaw == null || !roboClaw.IsOpen())
                {
                    try
                    {
                        roboClaw = new Roboclaw("COM3", 38400, 128); // Open the interface to the RoboClaw
                        roboClaw.Open();
                        roboClaw.ResetEncoders();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        //FIXTHIS
                        myParentScript.cusMsgBox2.Show("Com Port Operation Problem: " + ex.Message, "Com Port Error", MessageBoxButtons.OK);
                        //MessageBox.Show("Com Port Operation Problem: " + ex.Message, "Com Port Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        return false;
                        //throw;
                    }
                }
                return true;
            }

            public void MotorJog(short direction, int SleepTime)
            {
                if (roboClaw.IsOpen())
                {
                    //roboClaw.ST_M1Forward(20);
                    //System.Threading.Thread.Sleep(120);
                    //roboClaw.ST_M1Forward(0); // Stop the motor

                    short duty = (short)(20000 * direction);
                    uint accel = 500000;
                    SetMotorDutyAccel(duty, accel);
                    System.Threading.Thread.Sleep(SleepTime);
                    roboClaw.ST_M1Forward(0); // Stop the motor

                }
            }

            public void MotorGoForward(byte pwr)
            {
                if (roboClaw.IsOpen())
                {
                    roboClaw.ST_M1Forward(pwr); // Start the motor going forward at power 100
                }
            }

            public void MotorGoReverse(byte pwr)
            {
                if (roboClaw.IsOpen())
                {
                    roboClaw.ST_M1Backward(pwr); // Start the motor going forward at power 100
                }
            }

            public void SetMotorDutyAccel(short duty, uint accel)
            {
                if (roboClaw.IsOpen())
                {
                    roboClaw.M1DutyAccel(duty, accel);
                }
            }

            #endregion
        }

        public class Sphincter
        {
            //Instrument myins = new Instrument();

            OmegaScript myParentScript;
            //CStor
            public Sphincter(OmegaScript myScript)
            {
                myParentScript = myScript;
            }

            //Sphincter Support
            //=========================================================================================================================================================
            private SerialTransport _serialTransport;
            private CmdMessenger _cmdMessenger;

            private static string ScriptName = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            private static void ScriptLog(Severity mySeverity, string myLogMessage)
            {
                MAS.Framework.Logging.Lib.Logger.Log((MAS.Framework.Logging.Lib.Severity)mySeverity, myLogMessage, ScriptName);
            }

            private void AbortRequestEventHandler(object sender, EventArgs args)
            {
                ScriptLog(Severity.Info, "Stopping stepper...");
                StopMove();
            }

            public string Connect(List<string> ports)
            {
                bool exitloop = false;
                string stepperPort = "Null";
                foreach (string port in ports)
                {
                    //ScriptLog(Severity.Info, "Testing Port: " + port);
                    if (!exitloop)
                    {
                        try
                        {
                            Setup(port);
                            //ScriptLog(Severity.Info, "Setup Worked? " + Setup(port));
                            //ScriptLog(Severity.Info, "Firmware reading: " + GetFirmwareVersion());
                            if (GetFwVer(false).Contains("MAS Motor"))
                            {
                                stepperPort = port;
                                exitloop = true;
                            }
                            else
                            {
                                Cleanup();
                            }
                        }
                        catch (Exception e)
                        {
                            //do nothing
                        }
                    }
                }
                return stepperPort;
            }

            //public bool SoftSetup(string port)
            //{
            //    bool portWorking = false;
            //    //ScriptLog(Severity.Info, "Testing Port: " + port);
            //    try
            //    {
            //        Setup(port);
            //        //ScriptLog(Severity.Info, "Setup Worked? " + Setup(port));
            //        //ScriptLog(Severity.Info, "Firmware reading: " + GetFirmwareVersion());
            //        if (GetFwVer(false).Contains("MAS Motor"))
            //        {
            //            portWorking = true;
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        return false;
            //    }
            //    return portWorking;
            //}

            public bool Setup(string myComPort)
            {
                // Create Serial Port object
                // Note that for some boards (e.g. Sparkfun Pro Micro) DtrEnable may need to be true.
                _serialTransport = new SerialTransport
                {
                    CurrentSerialSettings = { PortName = myComPort, BaudRate = 115200, DtrEnable = false } // object initializer
                };

                // Initialize the command messenger with the Serial Port transport layer
                // Set if it is communicating with a 16- or 32-bit Arduino board
                _cmdMessenger = new CmdMessenger(_serialTransport, BoardType.Bit16);

                // Attach the callbacks to the Command Messenger
                AttachCommandCallBacks();

                // Attach to NewLinesReceived for logging purposes
                _cmdMessenger.NewLineReceived += NewLineReceived;

                // Attach to NewLineSent for logging purposes
                _cmdMessenger.NewLineSent += NewLineSent;

                // Start listening
                bool retVal = _cmdMessenger.Connect();

                return retVal;
            }

            public void SetupWithParams()
            {
                Setup("COM15");
                SetCurrentAxis(myParentScript.stepperDriver);
                SetMotorCurrentScaling(50);
                SetMaxSpeed(20000);
                SetMaxHomeSearchMove(300000);
                SetHomingSpeed(20000);
            }

            //public void SetupWithParams(string comPort)
            //{
            //    if (!myParentScript.abortRequested)
            //    {
            //        Setup(comPort);
            //        SetCurrentAxis(myParentScript.stepperDriver);
            //        SetMotorCurrentScaling(50);
            //        SetMaxSpeed(20000);
            //        SetMaxHomeSearchMove(300000);
            //        SetHomingSpeed(20000);
            //        myParentScript.AbortRequestEvent += AbortRequestEventHandler;
            //    }
            //}

            public void Cleanup()
            {
                // Stop listening
                _cmdMessenger.Disconnect();

                // Dispose Command Messenger
                _cmdMessenger.Dispose();

                // Dispose Serial Port object
                _serialTransport.Dispose();

            }

            #region Internal Functions

            /// Attach command call backs. 
            private void AttachCommandCallBacks()
            {
                _cmdMessenger.Attach(OnUnknownCommand);
                _cmdMessenger.Attach((int)Command.Acknowledge, OnAcknowledge);
                _cmdMessenger.Attach((int)Command.Error, OnError);
            }

            // ------------------  C A L L B A C K S ---------------------//

            // Called when a received command has no attached function.

            void OnUnknownCommand(ReceivedCommand arguments)
            {
                Console.WriteLine("Command without attached callback received");
            }

            // Callback function that prints that the Arduino has acknowledged
            void OnAcknowledge(ReceivedCommand arguments)
            {
                Console.WriteLine(" Arduino is ready");
            }

            // Callback function that prints that the Arduino has experienced an error
            void OnError(ReceivedCommand arguments)
            {
                Console.WriteLine(" Arduino has experienced an error");
            }

            // Log received line to console
            private void NewLineReceived(object sender, CommandEventArgs e)
            {
                Console.WriteLine(@"Received > " + e.Command.CommandString());
            }

            // Log sent line to console
            private void NewLineSent(object sender, CommandEventArgs e)
            {
                Console.WriteLine(@"Sent > " + e.Command.CommandString());
            }

            #endregion

            public string GetFwVer(bool isNumeric)
            {
                string FwVersion = "";

                // Create command GetFwVer, which will wait for a return command GetFwVer
                if (isNumeric)
                {
                    var command1 = new SendCommand((int)Command.GetFwVerNum, (int)Command.GetFwVerNum, 1000);

                    // Send command
                    var GetFwVerReply1 = _cmdMessenger.SendCommand(command1);

                    // Check if received a (valid) response
                    if (GetFwVerReply1.Ok)
                    {
                        // Read returned argument
                        float myReply = GetFwVerReply1.ReadFloatArg();
                        FwVersion = myReply.ToString("0.00");
                    }
                    //else
                    //    MessageBox.Show("No response to Firmware Request!");
                }
                else
                {
                    var command2 = new SendCommand((int)Command.GetFwVerStr, (int)Command.GetFwVerStr, 1000);

                    // Send command
                    var GetFwVerReply2 = _cmdMessenger.SendCommand(command2);

                    // Check if received a (valid) response
                    if (GetFwVerReply2.Ok)
                    {
                        // Read returned argument
                        string myReply = GetFwVerReply2.ReadStringArg();
                        FwVersion = myReply;
                    }
                    //else
                    //    MessageBox.Show("No response to Firmware Request!");
                }
                return FwVersion;
            }

            public void RotateContinuous(bool isDirPos)
            {
                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.MoveContinuous, (int)Command.MoveContinuous, 1000);

                // Add bool command argument
                command.AddArgument(isDirPos);

                // Send command
                var moveContinuous = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (moveContinuous.Ok)
                {
                    // Read returned argument
                    var myReply = moveContinuous.ReadBoolArg();
                }
            }

            public void MoveToAbsPosition(int myPos)
            {
                if (!myParentScript.abortRequested)
                {
                    //FW bug: if request to go to current position, the command times out!
                    int oldPos = int.Parse(GetPosition());
                    if (oldPos == myPos)
                        return;

                    // Create command, which will wait for a return command
                    //var command = new SendCommand((int)Command.MoveToPosition); //sends async
                    var command = new SendCommand((int)Command.MoveToPosition, (int)Command.GetMotorStatus, 5000);

                    // Add int command argument
                    command.AddArgument(myPos);

                    // Send command
                    var gotoMotorPositionReply = _cmdMessenger.SendCommand(command);

                    // Check if received a (valid) response
                    if (gotoMotorPositionReply.Ok)
                    {
                        // Read returned argument1 (axis number)
                        var myReply1 = gotoMotorPositionReply.ReadInt16Arg();
                        // Read returned argument2 (status)
                        var myReply2 = gotoMotorPositionReply.ReadInt16Arg();

                        //lblMoveToPosReply.Text = string.Format("axis: {0}, status: {1}", myReply1, myReply2);
                    }
                }
            }

            public void MoveToWaitDone2(int myPos)
            {
                //FW bug: if request to go to current position, the command times out!
                int oldPos = int.Parse(GetPosition());
                if (oldPos == myPos)
                    return;

                // Create command, which will wait for a return command
                //var command = new SendCommand((int)Command.MoveToPosition); //sends async
                var command = new SendCommand((int)Command.MoveToPosition, (int)Command.GetMotorStatus, 5000);

                // Add int command argument
                command.AddArgument(myPos);

                // Send command
                var gotoMotorPositionReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (gotoMotorPositionReply.Ok)
                {
                    // Read returned argument1 (axis number)
                    var myReply1 = gotoMotorPositionReply.ReadInt16Arg();
                    // Read returned argument2 (status)
                    var myReply2 = gotoMotorPositionReply.ReadInt16Arg();

                    //lblMoveToPosReply.Text = string.Format("axis: {0}, status: {1}", myReply1, myReply2);
                }

                var command2 = new SendCommand((int)Command.AwaitMotorMoveDone, (int)Command.GetMotorStatus, 90000);

                // Send command
                var WaitMotorDoneReply = _cmdMessenger.SendCommand(command2);

                // Check if received a (valid) response
                if (WaitMotorDoneReply.Ok)
                {
                    // Read returned argument
                    var myReply3 = WaitMotorDoneReply.ReadInt16Arg();

                }
            }

            //public void MoveToWaitDone(int myPos)
            //{
            //    MoveToAbsPosition(myPos);
            //    AwaitMoveDoneAsync(100000);
            //    ScriptLog(Severity.Info, GetPosition());
            //}

            public bool SetupWithParams(string comPort)
            {
                if (!myParentScript.abortRequested)
                {
                    try
                    {
                        Setup(comPort);
                        //ScriptLog(Severity.Info, "Setup Worked? " + Setup(port));
                        //ScriptLog(Severity.Info, "Firmware reading: " + GetFirmwareVersion());
                        if (GetADCReading() < 0.25)
                        {
                            return false;
                        }
                        else
                        {
                            //do nothing
                        }
                    }
                    catch (Exception e)
                    {
                        //do nothing
                    }
                    SetCurrentAxis(myParentScript.stepperDriver);
                    if (myParentScript.stepperDriver == 1)
                    {
                        SetMicroStepMode(8);
                        SetPower(1000);
                    }
                    else
                    {
                        SetMotorCurrentScaling(50);
                    }
                    SetMaxSpeed(20000);
                    SetMaxHomeSearchMove(300000);
                    SetHomingSpeed(20000);
                    myParentScript.AbortRequestEvent += AbortRequestEventHandler;
                    return true;
                }
                return false;
            } //updated because new stepper driver defaults to 1/4 steps so need to change to 1/8 steps at startup

            public void MoveToWaitDone(int myPos)
            {
                //myParentScript.myInstrument.motorNum++;
                //SetSensorOverride(3, true);
                MoveToAbsPosition(myPos);
                AwaitMoveDoneAsync(100000);
                //myParentScript.myInstrument.motorData.Add("\n" + myParentScript.myInstrument.motorNum + "," + DateTime.Now.ToString() + "," + GetPosition() + "," + myParentScript.grinderVertOffset);
                //myParentScript.myInstrument.newStep = true;
                //ScriptLog(Severity.Control, "Stepper at Position: " + GetPosition());
                //SetSensorOverride(3, false);
            } // added EOT sensor check to this

            public void MoveCheckInterlock(int distance)
            {
                int position = Convert.ToInt32(GetPosition());
                //SetSensorOverride(2, true);
                //SetSensorPolarity(2, true);
                while (position < distance && !myParentScript.abortRequested)
                {
                    MoveToAbsPosition(distance);
                    //monitoring loop
                    while (IsMoving())
                    {
                        Thread.Sleep(500);
                    }
                    position = Convert.ToInt32(GetPosition());
                    if (position < distance && !myParentScript.abortRequested)
                    {
                        //FIXTHIS
                        myParentScript.cusMsgBox2.Show("Close Door!", "Close Door!", MessageBoxButtons.OK);
                        //MessageBox.Show("Close Door!", "Close Door!", MessageBoxButtons.OK);
                    }
                }
                SetSensorOverride(2, false);
                SetSensorPolarity(2, false);
            }

            private void SetSensorOverride(Int16 mySensorNum, bool enabledState)
            {
                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SensorOverride, (int)Command.SensorOverride, 1000);

                // Add bool command argument
                command.AddArgument(mySensorNum);
                // Add bool command argument
                command.AddArgument(enabledState);

                // Send command
                var setSensorOverrideReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (setSensorOverrideReply.Ok)
                {
                    // Read returned argument
                    var myReply1 = setSensorOverrideReply.ReadInt16Arg();
                    var myReply2 = setSensorOverrideReply.ReadBoolArg();
                }
            }

            private void SetSensorPolarity(Int16 mySensorNum, bool polarity)
            {
                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SensorPolarity, (int)Command.SensorPolarity, 1000);

                // Add bool command argument
                command.AddArgument(mySensorNum);
                // Add bool command argument
                command.AddArgument(polarity);

                // Send command
                var setSensorPolarityReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (setSensorPolarityReply.Ok)
                {
                    // Read returned argument
                    var myReply1 = setSensorPolarityReply.ReadInt16Arg();
                    var myReply2 = setSensorPolarityReply.ReadBoolArg();
                }
            }

            public bool IsMoving()
            {
                bool moving = false;
                var command = new SendCommand((int)Command.GetMotorStatus, (int)Command.GetMotorStatus, 1000);

                // Send command
                var gotoMotorPositionReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (gotoMotorPositionReply.Ok)
                {
                    // Read returned argument1 (axis number)
                    var myReply1 = gotoMotorPositionReply.ReadInt16Arg();
                    // Read returned argument2 (status)
                    var myReply2 = gotoMotorPositionReply.ReadInt16Arg();

                    if (myReply2 == 1)
                    {
                        moving = true;
                    }

                }
                return moving;
            }

            private void SetMicroStepMode(Int16 mode)
            {
                //select a motor to which subsequent commands will be directed (1 to N)

                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SetMicroStepModeTMC2209, (int)Command.SetMicroStepModeTMC2209, 1000);

                // Add int16 command argument
                command.AddArgument(mode);

                // Send command
                var SetMicroStepModeReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (SetMicroStepModeReply.Ok)
                {
                    // Read returned argument
                    var myReply = SetMicroStepModeReply.ReadInt16Arg();
                }
            }

            private void SetPower(Int16 pow)
            {
                //select a motor to which subsequent commands will be directed (1 to N)

                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SetMotorCurrent, (int)Command.SetMotorCurrent, 1000);

                // Add int16 command argument
                command.AddArgument(pow);

                // Send command
                var SetMotorCurrentReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (SetMotorCurrentReply.Ok)
                {
                    // Read returned argument
                    var myReply = SetMotorCurrentReply.ReadInt16Arg();
                }
            }

            public void MoveToWaitDonePause(int myPos)
            {
                MoveToAbsPosition(myPos);
                AwaitMoveDone();
            }

            public void MoveToMultiSteps(int[] steps, int lagmSec)
            {
                ;
                for (int i = 0; i < steps.Count() && !myParentScript.abortRequested; i++)
                {
                    
                    MoveToWaitDone(steps[i]);
                    Sleep(lagmSec);
                    //Thread.Sleep(lagmSec);
                }
            }

            public void MoveToRelPosition(int myPos)
            {
                if (myPos == 0)
                    return;

                // Create command, which will wait for a return command
                //var command = new SendCommand((int)Command.MoveToPosition); //sends async
                var command = new SendCommand((int)Command.MoveRelative, (int)Command.GetMotorStatus, 5000);

                // Add int command argument
                command.AddArgument(myPos);

                // Send command
                var gotoMotorPositionReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (gotoMotorPositionReply.Ok)
                {
                    // Read returned argument1 (axis number)
                    var myReply1 = gotoMotorPositionReply.ReadInt16Arg();
                    // Read returned argument2 (status)
                    var myReply2 = gotoMotorPositionReply.ReadInt16Arg();

                    //lblMoveToPosReply.Text = string.Format("axis: {0}, status: {1}", myReply1, myReply2);
                }
            }

            private void MultiMoveToPosition(int myPos1, int myPos2, int myPos3, int myPos4)
            {

                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.MultiMoveToPosition, (int)Command.GetMotorStatus, 5000);

                // Add int command arguments
                command.AddArgument(myPos1);
                command.AddArgument(myPos2);
                command.AddArgument(myPos3);
                command.AddArgument(myPos4);

                // Send command
                var MultiMoveToPositionReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (MultiMoveToPositionReply.Ok)
                {
                    // Read returned argument1 (axis number)
                    var myReply1 = MultiMoveToPositionReply.ReadInt16Arg();
                    // Read returned argument2 (status)
                    var myReply2 = MultiMoveToPositionReply.ReadInt16Arg();

                }
            }

            private void SetMotorEnabledState(Int16 myMotorNum, bool enabledState)
            {
                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SetMotorEnabledState, (int)Command.SetMotorEnabledState, 1000);

                // Add bool command argument
                command.AddArgument(myMotorNum);
                // Add bool command argument
                command.AddArgument(enabledState);

                // Send command
                var setMotorEnabledReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (setMotorEnabledReply.Ok)
                {
                    // Read returned argument
                    var myReply1 = setMotorEnabledReply.ReadInt16Arg();
                    var myReply2 = setMotorEnabledReply.ReadBoolArg();
                }
            }

            public void GoToMotorXPosition(int x)
            {
                SetCurrentAxis(1);
                MoveToWaitDone(x); //X axis
            }

            public void GoToMotorYPosition(int y)
            {
                SetCurrentAxis(2);
                MoveToWaitDone(y); //Y axis
            }

            public void GoToMotorZPosition(int z)
            {
                SetCurrentAxis(3);
                MoveToWaitDone(z); //Z axis
            }

            //public void MoveXYZ(int x, int y, int z)
            //{
            //    //System.Diagnostics.Debugger.Launch();

            //    SetMotorEnabledState(4, false); //disable axis 4

            //    MultiMoveToPosition(x, y, z, 0);

            //    for (short i = 1; i < 4; i++)
            //    {
            //        SetCurrentAxis(i);
            //        AwaitMoveDone();
            //    }

            //    return;

            //    SetCurrentAxis(1);
            //    MoveToWaitDone(x); //X axis
            //    SetCurrentAxis(2);
            //    MoveToWaitDone(y); //Y axis
            //    SetCurrentAxis(3);
            //    MoveToWaitDone(z); //Z axis
            //}

            public void StopMove()
            {
                var command = new SendCommand((int)Command.MotorStop, (int)Command.GetMotorStatus, 2000);

                // Send command
                var gotoMotorPositionReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (gotoMotorPositionReply.Ok)
                {
                    // Read returned argument1 (axis number)
                    var myReply1 = gotoMotorPositionReply.ReadInt16Arg();
                    // Read returned argument2 (status)
                    var myReply2 = gotoMotorPositionReply.ReadInt16Arg();

                    string reply = string.Format("axis: {0}, status: {1}", myReply1, myReply2);
                }

            }

            public string GetStatus()
            {
                var command = new SendCommand((int)Command.GetMotorStatus, (int)Command.GetMotorStatus, 1000);

                // Send command
                var gotoMotorPositionReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (gotoMotorPositionReply.Ok)
                {
                    // Read returned argument1 (axis number)
                    var myReply1 = gotoMotorPositionReply.ReadInt16Arg();
                    // Read returned argument2 (status)
                    var myReply2 = gotoMotorPositionReply.ReadInt16Arg();

                    return string.Format("axis: {0}, status: {1}", myReply1, myReply2);
                }
                return "";
            }

            public void SetMaxHomeSearchMove(Int32 myMaxSearch)
            {
                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SetMaxHomeSearchMove, (int)Command.SetMaxHomeSearchMove, 1000);

                // Add int32 command argument
                command.AddArgument(myMaxSearch);

                // Send command
                var setMaxSearchReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (setMaxSearchReply.Ok)
                {
                    // Read returned argument
                    var myReply = setMaxSearchReply.ReadInt32Arg();
                }
            }

            public void SetHomingSpeed(Int32 myInitSpeed)
            {
                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SetHomingSpeed, (int)Command.SetHomingSpeed, 1000);

                // Add int32 command argument
                command.AddArgument(myInitSpeed);

                // Send command
                var setInitSpeedReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (setInitSpeedReply.Ok)
                {
                    // Read returned argument
                    var myReply = setInitSpeedReply.ReadInt32Arg();
                }
            }

            public void SetMaxTravel(Int32 myMaxTravel)
            {
                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SetMaxTravel, (int)Command.SetMaxTravel, 1000);

                // Add int32 command argument
                command.AddArgument(myMaxTravel);

                // Send command
                var setMaxTravelReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (setMaxTravelReply.Ok)
                {
                    // Read returned argument
                    var myReply = setMaxTravelReply.ReadInt32Arg();
                }
            }

            public void SetMaxSpeed(Int32 myMaxSpeed)
            {
                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SetMaxSpeed, (int)Command.SetMaxSpeed, 1000);

                // Add int32 command argument
                command.AddArgument(myMaxSpeed);

                // Send command
                var setMaxSpeedReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (setMaxSpeedReply.Ok)
                {
                    // Read returned argument
                    var myReply = setMaxSpeedReply.ReadInt32Arg();
                }
            }

            public void SetAccel(Int32 myAccelValue)
            {
                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SetAcceleration, (int)Command.SetAcceleration, 1000);

                // Add int32 command argument
                command.AddArgument(myAccelValue);

                // Send command
                var setAccelReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (setAccelReply.Ok)
                {
                    // Read returned argument
                    var myReply = setAccelReply.ReadInt32Arg();
                }
            }

            public void SetPolarity(bool isPolarityReversed)
            {
                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SetMovePolarity, (int)Command.SetMovePolarity, 1000);

                // Add bool command argument
                command.AddArgument(isPolarityReversed);

                // Send command
                var setPolarityReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (setPolarityReply.Ok)
                {
                    // Read returned argument
                    var myReply = setPolarityReply.ReadBoolArg();
                }
            }

            public string GetPosition()
            {
                try
                {
                    var command = new SendCommand((int)Command.GetPosition, (int)Command.GetPosition, 1000);

                    // Send command
                    var GetPositionReply = _cmdMessenger.SendCommand(command);

                    // Check if received a (valid) response
                    if (GetPositionReply.Ok)
                    {
                        // Read returned argument
                        Int32 myReply = GetPositionReply.ReadInt32Arg();
                        return myReply.ToString();
                    }
                    return "";
                }
                catch
                {
                    return "Something's wrong!";
                }
            }

            public double GetAvgADCReading(int cnt)
            {
                double SensorValue = 0;
                for (int i = 0; i < cnt; i++)
                {
                    SensorValue += GetADCReading();
                }
                SensorValue /= cnt;
                return Math.Round(SensorValue, 2);
            }

            public double GetADCReading()
            {
                //GetADCRawVoltage
                var command1 = new SendCommand((int)Command.GetADCRawVoltage, (int)Command.GetADCRawVoltage, 1000);

                // Send command
                var GetADCRawVoltage = _cmdMessenger.SendCommand(command1);

                // Check if received a (valid) response
                if (GetADCRawVoltage.Ok)
                {
                    // Read returned argument
                    float myReply = GetADCRawVoltage.ReadFloatArg();
                    return Math.Round(myReply, 2);
                }
                else
                {
                    return -999;
                    //    MessageBox.Show("No response!");
                }
            }

            private void setDigitalOutHigh(Int16 myOutputLine)
            {
                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SetOutHigh, (int)Command.SetOutHigh, 1000);

                // Add int16 command argument
                command.AddArgument(myOutputLine);

                // Send command
                var setDigitalOutHighReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (setDigitalOutHighReply.Ok)
                {
                    // Read returned argument
                    var myReply = setDigitalOutHighReply.ReadInt16Arg();
                }
            }

            private void setDigitalOutLow(Int16 myOutputLine)
            {
                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SetOutLow, (int)Command.SetOutLow, 1000);

                // Add int16 command argument
                command.AddArgument(myOutputLine);

                // Send command
                var setDigitalOutLowReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (setDigitalOutLowReply.Ok)
                {
                    // Read returned argument
                    var myReply = setDigitalOutLowReply.ReadInt16Arg();
                }
            }

            public void SetCurrentAxis(Int16 myMotorNum)
            {
                //select a motor to which subsequent commands will be directed (1 to N)

                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SetCurrentAxis, (int)Command.SetCurrentAxis, 1000);

                // Add int16 command argument
                command.AddArgument(myMotorNum);

                // Send command
                var SetCurrentAxisReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (SetCurrentAxisReply.Ok)
                {
                    // Read returned argument
                    var myReply = SetCurrentAxisReply.ReadInt16Arg();
                }
            }

            public void SetMotorCurrentScaling(Int16 scalingValue)
            {
                //scalingValue is 0 to 100 (as % of max) 25 or 50 or 75 or 100

                // Create command, which will wait for a return command
                var command = new SendCommand((int)Command.SetCurrentScaling, (int)Command.SetCurrentScaling, 1000);

                // Add int16 command argument
                command.AddArgument(scalingValue);

                // Send command
                var SetCurrentScalingReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (SetCurrentScalingReply.Ok)
                {
                    // Read returned argument
                    var myReply = SetCurrentScalingReply.ReadInt16Arg();
                }
            }

            public void Init()
            {
                // Create command, which will wait for a return command
                //var command = new SendCommand((int)Command.MoveToPosition); //sends async
                var command = new SendCommand((int)Command.InitializeAxis, (int)Command.GetMotorStatus, 60000);

                // Send command
                var initAxisReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (initAxisReply.Ok)
                {
                    // Read returned argument
                    var myReply = initAxisReply.ReadInt16Arg();
                }
            }

            public void Initialize()
            {
                if (!myParentScript.abortRequested)
                {
                    SetHomingSpeed(20000);
                    Init();
                    AwaitMoveDone();

                    //MoveToWaitDone(10000);

                    SetHomingSpeed(2800);
                    Init();
                    AwaitMoveDone();
                    SetHomingSpeed(20000);
                }
            }

            public void GoToMotorPosition(int myPos)
            {
                //FW bug: if request to go to current position, the command times out!
                //int oldPos = int.Parse(GetPosition());
                //if (oldPos == myPos)
                //    return;

                // Create command, which will wait for a return command
                //var command = new SendCommand((int)Command.MoveToPosition); //sends async
                var command = new SendCommand((int)Command.MoveToPosition, (int)Command.GetMotorStatus, 5000);

                // Add int command argument
                command.AddArgument(myPos);

                // Send command
                var gotoMotorPositionReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (gotoMotorPositionReply.Ok)
                {
                    // Read returned argument
                    var myReply = gotoMotorPositionReply.ReadInt16Arg();

                    //lblMoveToPosReply.Text = myReply.ToString();
                }
            }

            public void AwaitMoveDone()
            {
                var command = new SendCommand((int)Command.AwaitMotorMoveDone, (int)Command.GetMotorStatus, 90000);

                // Send command
                var gotoMotorPositionReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (gotoMotorPositionReply.Ok)
                {
                    // Read returned argument
                    var myReply = gotoMotorPositionReply.ReadInt16Arg();
                }
            }

            public void AwaitMoveDoneAsync(double myTimeOutSeconds)
            {
                var startTime = DateTime.Now;
                do
                {
                    if (!IsMotorRunning()) break;
                    Thread.Sleep(500);

                } while ((DateTime.Now - startTime).TotalSeconds < myTimeOutSeconds);

                //myParentScript.ScriptLog(Severity.Info, "Done waiting");
            }

            public bool IsMotorRunning()
            {
                var command = new SendCommand((int)Command.GetMotorStatus, (int)Command.GetMotorStatus, 1000);

                // Send command
                var gotoMotorPositionReply = _cmdMessenger.SendCommand(command);

                // Check if received a (valid) response
                if (gotoMotorPositionReply.Ok)
                {
                    // Read returned argument1 (axis number)
                    var myReply1 = gotoMotorPositionReply.ReadInt16Arg();
                    // Read returned argument2 (state)
                    // -1: new abs move initiated / 0: idle, ready / 1: busy (motor running) / 2: last move stopped
                    var myReply2 = gotoMotorPositionReply.ReadInt16Arg();

                    if (myReply2 == -1 || myReply2 == 1)
                        return true;
                }
                return false; //unknown
            }

            public void CheckMoveDone()
            {
                int moveDone = 0;
                //Thread.Sleep(2000);
                for (int i = 0; i < 90 && !myParentScript.abortRequested && moveDone < 2; i++)
                {
                    //ScriptLog(Severity.Info, GetStatus());
                    Thread.Sleep(1000);
                    if (GetStatus() == "axis: 0, status: 0" || GetStatus() == "axis: 0, status: 2")
                    {
                        moveDone++;
                    }
                }
                if (myParentScript.abortRequested)
                {
                    StopMove();
                }
            }

            public int MoveStep(int stepnum)
            {
                MoveToWaitDone(stepnum);
                return (stepnum);
            }

            public void Sleep(int milliSeconds)
            {
                bool quit = false;
                for (int z = 0; z < (milliSeconds / 250) && !quit; z++)
                {
                    Thread.Sleep(250);
                    if (myParentScript.abortRequested)
                    {
                        quit = true;
                    }
                }
            }

            public string GetFlagState()
            {
                var command = new SendCommand((int)Command.GetHomeFlagState, (int)Command.GetHomeFlagState, 1000);

                //Send command
                var GetHomeReply = _cmdMessenger.SendCommand(command);

                //Check if received a (valid) response
                if (GetHomeReply.Ok)
                {
                    //Read returned argument
                    Int32 myReply = GetHomeReply.ReadInt32Arg();
                    return myReply.ToString();
                }
                return "Unknown";
            }

            enum Command
            {
                Acknowledge,
                Error,
                FloatAddition,
                FloatAdditionResult,
                GetFwVerNum,
                GetFwVerStr,
                MoveToPosition,
                GetMotorStatus,
                MotorStop, // 8
                AwaitMotorMoveDone,
                InitializeAxis,
                SetMovePolarity,
                MoveRelative,
                GetPosition,
                SetMaxTravel,
                SetMaxSpeed, // 15
                SetCurrentScaling, // 16
                SetCurrentAxis, // 17
                MultiMoveToPosition,
                MultiMoveRelative,
                SetAcceleration, // 20 - Command to set acceleration value in steps/sec/sec (default is 50,000)
                MoveContinuous, // 21 - Command to rotate until stopped
                ResetPosition, // 22 - Command to reset the current motor position to zero
                SetMaxHomeSearchMove, // 23 - Command to set max search distance for home
                SetHomingSpeed, // 24 - Command to set homing speed
                GetHomeFlagState, // 25 - Command to get home flag state for current axis

                GetLastInitPosition, // 26 - Command to get last axis init position
                SetMotorEnabledState, // 27 - Command to set flag specifying whether motor is enabled

                LEDsOff, // 28 - Command to set color of all NeoPixels to Off
                LEDsIdle, // 29 - Command to set color of all NeoPixels to Blue
                LEDsRun, // 30 - Command to send green Wipe pattern to NeoPixels
                LEDsError, // 31 - Command to send red Flash pattern to NeoPixels

                GetADCRawVoltage, // 32 Command to get ADC value in unscaled units -- added in V1.0.18

                SetOutHigh, // 33 Command to set an output pin high (1 to 10)
                SetOutLow, // 34 Command to set an output pin low (1 to 10)

                SetLimitSwitchPolarity, // 35 Command to set polarity of switches used; T=default (false if blocked)
                SetCurrentLimitSwitch, // 36 Command to set currently active limit switch (it is auto-set on axis change)
                HInitializeXY, // 37 Command to init X axis of H-Bot using optical switch
                HMoveRelative, // 38 Command to move X,Y of H-Bot to relative target position
                HMoveToPosition, // 39 Command to move X,Y of H-Bot to absolute target position
                HGetXY, // 40 Command to get X,Y coordinates of H-Bot

                GetDebugStr, // 41 Command to get Debug string
                HMoveDoneMssg, // 42 Message to communicate that an H-move is complete: HInitializeXY, HMoveRelative, HMoveToPosition

                SetHoldCurrentScaling, // 43 Command to set motor hold current scaling (% of max current)
                SetMicroStepModeL6470, // 44 Command to set microstep mode: 0 to 7 == full to 128

                //Different firmware versions differed with respect to #43 & #44

                LEDsSetColor, //45 Command to set color of each NeoPixel (3) (not used in this version of sw)
                EnableEOT, //46 Command to enable all EOT sensors to stop motion on change (not used in this version of sw)
                DisableEOT, //47 Command to disable all EOT sensors to stop motion on change (not used in this version of sw)

                SetStallThreshold, //48 Command to set TMC2209 stall threshold value
                SetMotorCurrent, //49 Command to set TMC2209 motor current in mA
                SetMicroStepModeTMC2209, //50 Command to set TMC2209 microstep mode -- param is 2,4,8,16,...,256
                StopOnStall, //51 Command to set TMC2209 to stop when threshold exceeded -- param T|F
                InitTMC2209, //52 Command to initialize the TMC2209

                SensorOverride, //53 Command to stop on any of the 8 sensors
                SensorPolarity //54 Command to set polarity of any of the 8 sensors

            };
        }

        public class Laird
        {
            //Instrument myins = new Instrument();

            OmegaScript myParentScript;
            //CStor
            public Laird(OmegaScript myScript)
            {
                myParentScript = myScript;
            }

            public string tempScriptName = "SinguScript2.4";
            public string tempFolderName = @"C:\ProgramData\LabScript\LogFiles\TempLogs\";
            public string tempFileSuffix;
            public string tempFileName;
            public List<string> tempData = new List<string>();
            public int tempNum = 0;
            //public string filename = @"C:\LabScript\TempLogs\" + DateTime.Now.ToString("yyyy_MM_dd_HHmmss") + "_TempLog.csv";
            //Laird Support

            private static string ScriptName = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            private static void ScriptLog(Severity mySeverity, string myLogMessage)
            {
                MAS.Framework.Logging.Lib.Logger.Log((MAS.Framework.Logging.Lib.Severity)mySeverity, myLogMessage, ScriptName);
            }

            public string LairdGetMessage()
            {
                string LairdMessage = "UsingLocalMethods Properly";
                return LairdMessage;
            }

            LairdController tempBlock = new LairdController();

            #region Default Laird Values
            //Params with default values:
            //====================================
            string TCportName = "COM18";
            int TCbaudRate = 115200;

            double TCgain = 1.0;
            double TCoffset = 0; //RogerM had set to -45.5
            double TCcoeffA = 1.39691809e-03; //RogerM had set to 1.52786595e-03
            double TCcoeffB = 2.37825807e-04; //RogerM had set to 2.43706279e-04
            double TCcoeffC = 9.37265341e-08; //RogerM had set to -3.56562509e-07

            double PidPval = 14.0;
            double PidIval = 100.0;
            double PidDval = 700.0;
            double PidTRval = 1.0;
            double PidTEval = 1.0;
            double PidLimit = 100.0;

            double PwrDeadBand = 3; //RogerM had set to 1.5
            double PwrMax = 100; //RogerM had set to 25.0
            double PwrCoolGain = 1.0;
            double PwrHeatGain = 1.0;
            double PwrDecay = 0.1;

            double FanTargetTemp = 20.0;
            double FanDeadBand = 8.0;
            double FanLSHyst = 4.0;
            double FanHSHyst = 2.0;
            double FanLSVolts = 12.0;
            double FanHSVolts = 24.0;

            int RegulatorMode = 6; //PID Control
            int FanMode = 1; //0 = Off -- not necessary; just for illustration
            double RegulatorTargetTemp = 0.0; //Target Temperature to control to (Regulator)
            double SampleRate = 0.05; //Sampling rate (Regulator) -- default is 0.05sec
                                      //====================================
            #endregion

            private void PortReconnectedHandler(object sender, EventArgs args)
            {
                //New event handler 3/12/21

                myParentScript.PortReconnected(); //notify the script that the port was reconnected
            }

            public bool Connect()
            {
                tempBlock.PortReconnected += PortReconnectedHandler; //<-------------------- New event 3/12/21

                bool retVal = tempBlock.Connect(TCportName, TCbaudRate);
                return retVal;
            } //works

            bool IsLairdController(string myPort)
            {
                SerialPort mySerialPort = new SerialPort(myPort, 115200, Parity.None, 8, StopBits.One);
                mySerialPort.Open();
                if (mySerialPort.IsOpen)
                {
                    string message = "$v\r";

                    mySerialPort.Write(message);

                    Thread.Sleep(100);
                    if (mySerialPort.BytesToRead > 0)
                    {
                        string myData = mySerialPort.ReadExisting();
                        if (myData.Contains("SC_v"))
                        {
                            mySerialPort.Close();
                            return true;
                        }
                    }
                    mySerialPort.Close();
                }

                return false;
            }

            public string Connect(List<string> ports)
            {
                bool exitloop = false;
                string lairdport = "";
                foreach (string port in ports)
                {
                    if (!exitloop)
                    {
                        try
                        {
                            ScriptLog(Severity.Info, "Connecting to port " + port);
                            //ModSetup(port);
                            //Cleanup();
                            bool connected = IsLairdController(port);
                            ScriptLog(Severity.Info, "Connection successful? " + connected);
                            //ModSetup(port);
                            if (connected)
                            {
                                exitloop = true;

                                //string firmwarever = GetFirmwareVersion();
                                //double temp = GetTemperature();
                                //ScriptLog(Severity.Info, "Temp on " + port + " is: " + temp);
                                //ScriptLog(Severity.Info, "Firmware on " + port + " is: " + firmwarever);
                                //if (temp > 0 && temp < 200)
                                //{
                                //    ScriptLog(Severity.Info, "TEC detected");
                                //    lairdport = port;
                                //    exitloop = true;
                                //    ScriptLog(Severity.Info, "Checking Temp Next...");
                                //}
                                //else
                                //{
                                //    ScriptLog(Severity.Info, "Try Again...");
                                //    Cleanup();
                                //    ScriptLog(Severity.Info, "Next Port...");
                                //}
                                //if (firmwarever == "")
                                //{
                                //    ScriptLog(Severity.Info, "Try Again...");
                                //    //Cleanup();
                                //    ScriptLog(Severity.Info, "Next Port...");
                                //}
                                //else if (firmwarever.Contains("SSCI"))
                                //{
                                //    ScriptLog(Severity.Info, "TEC detected");
                                //    lairdport = port;
                                //    exitloop = true;
                                //    ScriptLog(Severity.Info, "Checking Temp Next...");
                                //}
                                //else
                                //{
                                //    ScriptLog(Severity.Info, "Try Again...");
                                //    //Cleanup();
                                //    ScriptLog(Severity.Info, "Next Port...");
                                //}
                                //exitloop = true;
                                lairdport = port;
                            }
                            else
                            {
                                //Cleanup();
                                ScriptLog(Severity.Info, "Oops, that wasn't the right port!");
                            }
                        }
                        catch (Exception e)
                        {
                            ScriptLog(Severity.Info, "Could not get or match firmware...");
                            ScriptLog(Severity.Info, e.ToString());
                        }
                    }
                }
                return lairdport;
            }

            public bool SoftSetup(string portname)
            {
                bool connected = IsLairdController(portname);
                return connected;
            }

            public string ResetRegulator()
            {
                string myReply;
                tempBlock.StopRegulator(); //stops fan by default
                System.Threading.Thread.Sleep(500);
                myReply = tempBlock.Reset();
                return myReply;
            }

            public bool Setup()
            {
                tempBlock.PortReconnected += PortReconnectedHandler; //<-------------------- New event 3/12/21

                //GetTempControlParams();
                bool retVal = tempBlock.Connect(TCportName, TCbaudRate);

                if (!retVal) return retVal; //failed

                tempBlock.StopRegulator(); //stops fan by default
                System.Threading.Thread.Sleep(500);

                string myReply = tempBlock.Reset();
                //Console.WriteLine("     " + myReply + "      <=============== RESET REPLY");
                System.Threading.Thread.Sleep(3000);
                //=============================================================

                //get the firmware version and record to log file:
                //=============================================================
                myReply = tempBlock.GetVersion();
                //myParentScript.ScriptLog(Severity.Info, "FW Version: " + myReply);
                //=============================================================

                tempBlock.SetTcCoefficients(TCgain, TCoffset, TCcoeffA, TCcoeffB, TCcoeffC);
                tempBlock.SetPidValues(PidPval, PidIval, PidDval, PidTRval, PidTEval, PidLimit);
                tempBlock.SetPowerValues(PwrDeadBand, PwrMax, PwrCoolGain, PwrHeatGain, PwrDecay);
                tempBlock.SetFanValues(FanTargetTemp, FanDeadBand, FanLSHyst, FanHSHyst, FanLSVolts, FanHSVolts);
                tempBlock.SetControlValues(RegulatorMode, FanMode, RegulatorTargetTemp, FanTargetTemp, SampleRate);

                return true;
            }

            public bool Setup(string portname)
            {
                if (!myParentScript.abortRequested)
                {
                    //GetTempControlParams();
                    TCportName = portname;
                    bool retVal = tempBlock.Connect(TCportName, TCbaudRate);

                    if (!retVal) return retVal; //failed

                    tempBlock.PortReconnected += PortReconnectedHandler; //<-------------------- New event 3/12/21

                    tempBlock.StopRegulator(); //stops fan by default
                    System.Threading.Thread.Sleep(500);

                    string myReply = tempBlock.Reset();
                    //Console.WriteLine("     " + myReply + "      <=============== RESET REPLY");
                    System.Threading.Thread.Sleep(3000);
                    //=============================================================

                    //get the firmware version and record to log file:
                    //=============================================================
                    myReply = tempBlock.GetVersion();
                    //myParentScript.ScriptLog(Severity.Info, "FW Version: " + myReply);
                    //=============================================================

                    tempBlock.SetTcCoefficients(TCgain, TCoffset, TCcoeffA, TCcoeffB, TCcoeffC);
                    tempBlock.SetPidValues(PidPval, PidIval, PidDval, PidTRval, PidTEval, PidLimit);
                    tempBlock.SetPowerValues(PwrDeadBand, PwrMax, PwrCoolGain, PwrHeatGain, PwrDecay);
                    tempBlock.SetFanValues(FanTargetTemp, FanDeadBand, FanLSHyst, FanHSHyst, FanLSVolts, FanHSVolts);
                    tempBlock.SetControlValues(RegulatorMode, FanMode, RegulatorTargetTemp, FanTargetTemp, SampleRate);

                    tempBlock.SendCmdGetResponse("RW");
                }
                return true;
            }

            public bool Setup(string portname, int totalSensors)
            {
                tempBlock.PortReconnected += PortReconnectedHandler; //<-------------------- New event 3/12/21

                //GetTempControlParams();
                TCportName = portname;
                bool retVal = tempBlock.Connect(TCportName, TCbaudRate);

                if (!retVal) return retVal; //failed

                tempBlock.StopRegulator(); //stops fan by default
                System.Threading.Thread.Sleep(500);

                string myReply = tempBlock.Reset();
                //Console.WriteLine("     " + myReply + "      <=============== RESET REPLY");
                System.Threading.Thread.Sleep(3000);
                //=============================================================

                //get the firmware version and record to log file:
                //=============================================================
                myReply = tempBlock.GetVersion();
                //myParentScript.ScriptLog(Severity.Info, "FW Version: " + myReply);
                //=============================================================

                //tempBlock.SetTcCoefficients(TCgain, TCoffset, TCcoeffA, TCcoeffB, TCcoeffC);
                for (int i = 1; i <= totalSensors; i++)
                {
                    tempBlock.SetTcCoefficients(i, TCgain, TCoffset, TCcoeffA, TCcoeffB, TCcoeffC);
                }
                tempBlock.SetPidValues(PidPval, PidIval, PidDval, PidTRval, PidTEval, PidLimit);
                tempBlock.SetPowerValues(PwrDeadBand, PwrMax, PwrCoolGain, PwrHeatGain, PwrDecay);
                tempBlock.SetFanValues(FanTargetTemp, FanDeadBand, FanLSHyst, FanHSHyst, FanLSVolts, FanHSVolts);
                tempBlock.SetControlValues(RegulatorMode, FanMode, RegulatorTargetTemp, FanTargetTemp, SampleRate);

                tempBlock.SendCmdGetResponse("RW");

                return true;
            }

            public double GetTemperature(int sensor)
            {
                double myTemp;
                try
                {
                    myTemp = tempBlock.GetTemperature(sensor);
                    return myTemp;
                    //return Math.Round(myTemp, 1);
                }
                catch (Exception e)
                {
                    //MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    ScriptLog(Severity.Error, "Temperature Read Error: " + e.Message);
                    myTemp = 0.0;
                    return myTemp;
                }
            }

            public double GetTemperatureNoCheck(int sensor)
            {
                double myTemp;
                try
                {
                    myTemp = tempBlock.GetTemperature(sensor);
                    return myTemp;
                    //return Math.Round(myTemp, 1);
                }
                catch (Exception e)
                {
                    //MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    ScriptLog(Severity.Error, "Temperature Read Error: " + e.Message);
                    myTemp = 0.0;
                    return myTemp;
                }
            }

            public bool SetTECParameters()
            {
                tempBlock.SetTcCoefficients(TCgain, TCoffset, TCcoeffA, TCcoeffB, TCcoeffC);
                tempBlock.SetPidValues(PidPval, PidIval, PidDval, PidTRval, PidTEval, PidLimit);
                tempBlock.SetPowerValues(PwrDeadBand, PwrMax, PwrCoolGain, PwrHeatGain, PwrDecay);
                tempBlock.SetFanValues(FanTargetTemp, FanDeadBand, FanLSHyst, FanHSHyst, FanLSVolts, FanHSVolts);
                tempBlock.SetControlValues(RegulatorMode, FanMode, RegulatorTargetTemp, FanTargetTemp, SampleRate);
                return true;
            }

            public bool SetTECParameters(double setP)
            {
                RegulatorTargetTemp = setP;
                tempBlock.SetTcCoefficients(TCgain, TCoffset, TCcoeffA, TCcoeffB, TCcoeffC);
                tempBlock.SetPidValues(PidPval, PidIval, PidDval, PidTRval, PidTEval, PidLimit);
                tempBlock.SetPowerValues(PwrDeadBand, PwrMax, PwrCoolGain, PwrHeatGain, PwrDecay);
                tempBlock.SetFanValues(FanTargetTemp, FanDeadBand, FanLSHyst, FanHSHyst, FanLSVolts, FanHSVolts);
                tempBlock.SetControlValues(RegulatorMode, FanMode, RegulatorTargetTemp, FanTargetTemp, SampleRate);
                return true;
            }

            public string GetFirmwareVersion()
            {
                string myReply;
                myReply = tempBlock.GetVersion();
                return myReply;
            }

            public void ControlTemperature()
            {
                tempBlock.RunRegulator(); //starts fan by default
            }

            public void ControlTemperature(double myTemp)
            {
                if (!myParentScript.abortRequested)
                {
                    CreateTempLog();

                    tempBlock.SetRegulatorTargetTemperature(myTemp);
                    tempBlock.RunRegulator(); //starts fan by default
                }
            }

            public void ffpeControlTemperature(double myTemp)
            {
                if (!myParentScript.abortRequested)
                {
                    CreateTempLog();

                    tempBlock.SetRegulatorTargetTemperature(myTemp);
                    //tempBlock.RunRegulator(); //starts fan by default
                }
            }

            public void SetParamsAndControlTemperature(double myTemp, string style)
            {
                if (!myParentScript.abortRequested && style == "ffpe")
                {
                    CreateTempLog();
                    tempBlock.SetPidValues(PidPval, PidIval, PidDval, PidTRval, PidTEval, PidLimit);
                    tempBlock.SetPowerValues(PwrDeadBand, PwrMax, PwrCoolGain, PwrHeatGain, PwrDecay);
                    tempBlock.SetFanValues(FanTargetTemp, FanDeadBand, FanLSHyst, FanHSHyst, FanLSVolts, FanHSVolts);
                    tempBlock.SetControlValues(RegulatorMode, FanMode, myTemp, FanTargetTemp, SampleRate);

                    tempBlock.SetRegulatorTargetTemperature(myTemp);
                    tempBlock.RunRegulator(); //starts fan by default
                    tempBlock.SetFanMode(0);
                }
                else
                {
                    CreateTempLog();
                    tempBlock.SetPidValues(PidPval, PidIval, PidDval, PidTRval, PidTEval, PidLimit);
                    tempBlock.SetPowerValues(PwrDeadBand, PwrMax, PwrCoolGain, PwrHeatGain, PwrDecay);
                    tempBlock.SetFanValues(FanTargetTemp, FanDeadBand, FanLSHyst, FanHSHyst, FanLSVolts, FanHSVolts);
                    tempBlock.SetControlValues(RegulatorMode, FanMode, myTemp, FanTargetTemp, SampleRate);

                    tempBlock.SetRegulatorTargetTemperature(myTemp);
                    tempBlock.RunRegulator(); //starts fan by default
                }
            }

            public double GetTemperature()
            {
                AddTempData();
                double myTemp;
                try
                {
                    myTemp = tempBlock.GetTemperature();
                    return myTemp;
                    //return Math.Round(myTemp, 1);
                }
                catch (Exception e)
                {
                    //MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    ScriptLog(Severity.Error, "Temperature Read Error: " + e.Message);
                    myTemp = 0.0;
                    return myTemp;
                }
            }

            public double GetTemperatureNoCheck()
            {
                double myTemp;
                try
                {
                    myTemp = tempBlock.GetTemperature();
                    return myTemp;
                    //return Math.Round(myTemp, 1);
                }
                catch (Exception e)
                {
                    //MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    ScriptLog(Severity.Error, "Temperature Read Error: " + e.Message);
                    myTemp = 0.0;
                    return myTemp;
                }
            }

            public string GetTemperaturetoString()
            {
                string myTempString;
                try
                {
                    double myTemp = tempBlock.GetTemperature();
                    myTemp = Math.Round(myTemp, 1);
                    myTempString = myTemp.ToString();
                }
                catch
                {
                    myTempString = "UNAVAILABLE";
                }
                return myTempString;
            }

            public void Cleanup()
            {
                ScriptLog(Severity.Info, "Stopping Regulator...");
                tempBlock.StopRegulator(); //stops fan by default
                ScriptLog(Severity.Info, "Stopped Regulator, now Disconnecting Regulator");
                tempBlock.Dispose();
                //tempBlock.Disconnect();
                ScriptLog(Severity.Info, "Regulator Cleaned Up");
            }

            public void Stop()
            {
                tempBlock.StopRegulator();
            }

            public void CreateTempLog()
            {
                //create file name
                tempFileSuffix = @"_" + DateTime.Now.ToString("yyyy_MM_dd") + "_TempLog.csv";
                tempFileName = @tempFolderName + tempScriptName + tempFileSuffix;
                //create folder if one doesn't already exist
                System.IO.Directory.CreateDirectory(tempFolderName);
                //create file if one doesn't already exist and add header
                if (!System.IO.File.Exists(tempFileName))
                {
                    System.IO.File.Create(tempFileName).Close();
                    System.IO.File.AppendAllText(tempFileName,"#,Date/Time,Block Set Point, Block Temp, Ambient Temp, HeatSink Temp\n");
                }
            }

            public void AddTempData()
            {
                //update tempNum variable
                tempNum++;
                //add data to tempData list
                tempData.Add(tempNum + "," + DateTime.Now.ToString() + "," + "<Set Point>" + "," + GetTemperatureNoCheck() + "," + GetTemperatureNoCheck(2) + "," + GetTemperatureNoCheck(3) + "\n");

            }

            public void RecordTempData()
            {
                //convert list to string
                string tempLog = string.Join("", tempData);
                //record string to file
                System.IO.File.AppendAllText(tempFileName, tempLog);
                //clear list of temp data and set tempNum back to 0 to start over
                tempData.Clear();
                tempNum = 0;
            }

            public void MonitorandTurnDownTemp(string runTempChoice, int turnDownTime, double durationTemp, int interval)
            {
                ScriptLog(Severity.Control, "Monitoring Temperature...");
                int i = 0;
                double ambT = -900;
                double newSetT;
                while (!myParentScript.myInstrument.incStarted && !myParentScript.abortRequested)
                {
                    if (i % 10 == 0)
                    {
                        ScriptLog(Severity.Control, "Block Temperature = " + GetTemperature());
                        ScriptLog(Severity.Control, "Ambient Temperature = " + GetTemperature(2));
                        ScriptLog(Severity.Control, "Heat Sink Temperature = " + GetTemperature(3));
                    }
                    i++;
                    Thread.Sleep(interval);
                }
                if (runTempChoice == "Heat" && !myParentScript.abortRequested)
                {
                    ScriptLog(Severity.Control, "Checking temp for " + turnDownTime + " secs before turning down...");
                    //var startTime = DateTime.UtcNow;
                    //while (Convert.ToInt16(DateTime.UtcNow - startTime) < turnDownTime)
                    for (i = 0; i < turnDownTime && !myParentScript.myInstrument.incFinished && !myParentScript.abortRequested; i++)
                    {
                        if (i % 10 == 0)
                        {
                            ScriptLog(Severity.Control, "Block Temperature = " + GetTemperature());
                            ScriptLog(Severity.Control, "Ambient Temperature = " + GetTemperature(2));
                            ScriptLog(Severity.Control, "Heat Sink Temperature = " + GetTemperature(3));
                        }
                        Thread.Sleep(interval);
                    }

                    if (!myParentScript.myInstrument.incFinished && !myParentScript.abortRequested)
                    {
                        ambT = -900;
                        while (ambT < -100)
                        {
                            ambT = GetTemperature(2);
                        }
                        durationTemp = 37 + ((35 - ambT) * 0.6);
                        ScriptLog(Severity.Control, "Changing Block Target Temp To " + durationTemp.ToString());
                        ControlTemperature(durationTemp);
                    }
                }
                while (!myParentScript.myInstrument.incFinished && !myParentScript.abortRequested)
                {
                    if (i % 10 == 0)
                    {
                        ambT = -100;
                        while (ambT < -10)
                        {
                            ambT = GetTemperature(2);
                        }
                        ScriptLog(Severity.Control, "Block Temperature = " + GetTemperature());
                        ScriptLog(Severity.Control, "Ambient Temperature = " + ambT);
                        ScriptLog(Severity.Control, "Heat Sink Temperature = " + GetTemperature(3));
                        if (runTempChoice == "Heat" && i % 30 == 0)
                        {
                            newSetT = 37 + ((35 - ambT) * 0.6);
                            ControlTemperature(newSetT);
                            ScriptLog(Severity.Control, "Changing Block Target Temp To " + newSetT);
                        }
                    }
                    i++;
                    Thread.Sleep(interval);
                }
                if (runTempChoice == "Heat" && !myParentScript.abortRequested)
                {
                    ControlTemperature(37);
                }
            }

            public void CellTempToggle_ProtoExplorer(string runTempChoice, int turnDownTime, double durationTemp, int interval)
            {
                ScriptLog(Severity.Control, "|-|+| Monitoring Temperature in Info Log...");
                int i = 0;
                double ambT = -900;
                double newSetT;
                //myParentScript.myInstrument.incStarted = true;
                
                //while (!myParentScript.myInstrument.incStarted && !myParentScript.abortRequested)
                //{
                //    if (i % 10 == 0)
                //    {
                //        ScriptLog(Severity.Info, $"|-|- Block Temp: {GetTemperature()}C, Ambient Temp: {GetTemperature(2)}C, Heat Sink Temp: {GetTemperature(3)}C");
                //        //ScriptLog(Severity.Control, "Ambient Temperature = " + GetTemperature(2));
                //        //ScriptLog(Severity.Control, "Heat Sink Temperature = " + GetTemperature(3));
                //    }
                //    i++;
                //    Thread.Sleep(interval);
                //}
                if (runTempChoice == "rampDown" && myParentScript.myInstrument.incStarted)
                {
                    ScriptLog(Severity.Info, "Checking temp for " + turnDownTime + " secs before turning down...");
                    //var startTime = DateTime.UtcNow;
                    //while (Convert.ToInt16(DateTime.UtcNow - startTime) < turnDownTime)
                    for (i = 0; i < turnDownTime && !myParentScript.myInstrument.incFinished && !myParentScript.abortRequested; i++)
                    {
                        if (i % 10 == 0)
                        {
                            ScriptLog(Severity.Info, $"|-||- Block Temp: {GetTemperature()}C, Ambient Temp: {GetTemperature(2)}C, Heat Sink Temp: {GetTemperature(3)}C");
                            //ScriptLog(Severity.Control, "Block Temperature = " + GetTemperature());
                            //ScriptLog(Severity.Control, "Ambient Temperature = " + GetTemperature(2));
                            //ScriptLog(Severity.Control, "Heat Sink Temperature = " + GetTemperature(3));
                        }
                        Thread.Sleep(interval);
                    }

                    if (!myParentScript.myInstrument.incFinished && !myParentScript.abortRequested)
                    {
                        ambT = -900;
                        while (ambT < -100)
                        {
                            ambT = GetTemperature(2);
                        }
                        durationTemp = 37 + ((35 - ambT) * 0.6);
                        ScriptLog(Severity.Info, "|- Changing Block Target Temp To " + durationTemp.ToString());
                        ControlTemperature(durationTemp);
                    }
                }
                while (!myParentScript.myInstrument.incFinished && !myParentScript.abortRequested)
                {
                    if (i % 10 == 0)
                    {
                        ambT = -100;
                        while (ambT < -10)
                        {
                            ambT = GetTemperature(2);
                        }
                        ScriptLog(Severity.Info, $"|-|||- Block Temp: {GetTemperature()}C, Ambient Temp: {ambT}C, Heat Sink Temp: {GetTemperature(3)}C");
                        //ScriptLog(Severity.Control, "Block Temperature = " + GetTemperature());
                        //ScriptLog(Severity.Control, "Ambient Temperature = " + ambT);
                        //ScriptLog(Severity.Control, "Heat Sink Temperature = " + GetTemperature(3));
                        if (runTempChoice == "rampDown" && i % 30 == 0)
                        {
                            newSetT = 37 + ((35 - ambT) * 0.6);
                            ControlTemperature(newSetT);
                            ScriptLog(Severity.Info, "|-|+|=| Changing Block Target Temp To " + newSetT);
                        }
                    }
                    i++;
                    Thread.Sleep(interval);
                }
                if (runTempChoice == "rampDown" && myParentScript.myInstrument.incFinished)
                {
                    ScriptLog(Severity.Info, "Incubation Finished |-|+|=| Changing Block Target Temp To " + 37);
                    ControlTemperature(37);
                }
            }

            public void monitorTemp(int interval)
            {
                ScriptLog(Severity.Control, "|-|+| Monitoring Temperature in Info Log...");

                int i = 0;
                while (!myParentScript.myInstrument.incStarted && !myParentScript.abortRequested)
                {
                    if (i % 10 == 0)
                    {
                        ScriptLog(Severity.Info, $"|-|- Block Temp: {GetTemperature()}C, Ambient Temp: {GetTemperature(2)}C, Heat Sink Temp: {GetTemperature(3)}C");
                        //ScriptLog(Severity.Control, "Ambient Temperature = " + GetTemperature(2));
                        //ScriptLog(Severity.Control, "Heat Sink Temperature = " + GetTemperature(3));
                    }
                    i++;
                    Thread.Sleep(interval);
                }

            }





            public void TurnTempBackToZeroC()
            {
                double blockTemp = -900;
                //check that block temp in fact reaches above 0C
                while (blockTemp < 0)
                {
                    blockTemp = -900;
                    while (blockTemp < -100)
                    {
                        blockTemp = GetTemperature();
                    }
                    ScriptLog(Severity.Control, "Block Temperature = " + blockTemp);
                    Thread.Sleep(1000);
                }
            }

            //public void GetTempControlParams()
            //{
            //    //Get params from Json file:
            //    try
            //    {
            //        string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            //        string jsonSysSettingsPath = string.Format(@"{0}\{1}\{2}\{3}", progData, "LabScript", "CompiledScripts", "TemperatureControlParameters.json");

            //        var jsonSysSettings = JObject.Parse(File.ReadAllText(jsonSysSettingsPath));

            //        JToken jRoot = jsonSysSettings.Root;
            //        string ControllerType = (string)jRoot["Type"];

            //        JToken jNode = jsonSysSettings["ComPort"];
            //        string TCportName = (string)jNode["TCportName"];
            //        int TCbaudRate = (int)jNode["TCbaudRate"];

            //        jNode = jsonSysSettings["ThermisterCoefficients"];
            //        TCgain = (double)jNode["TCgain"];
            //        TCoffset = (double)jNode["TCoffset"];
            //        TCcoeffA = (double)jNode["TCcoeffA"];
            //        TCcoeffB = (double)jNode["TCcoeffB"];
            //        TCcoeffC = (double)jNode["TCcoeffC"];

            //        jNode = jsonSysSettings["PidValues"];
            //        PidPval = (double)jNode["PidPval"];
            //        PidIval = (double)jNode["PidIval"];
            //        PidDval = (double)jNode["PidDval"];
            //        PidTRval = (double)jNode["PidTRval"];
            //        PidTEval = (double)jNode["PidTEval"];
            //        PidLimit = (double)jNode["PidLimit"];

            //        jNode = jsonSysSettings["PowerValues"];
            //        PwrDeadBand = (double)jNode["PwrDeadBand"];
            //        PwrMax = (double)jNode["PwrMax"];
            //        PwrCoolGain = (double)jNode["PwrCoolGain"];
            //        PwrHeatGain = (double)jNode["PwrHeatGain"];
            //        PwrDecay = (double)jNode["PwrDecay"];

            //        jNode = jsonSysSettings["FanValues"];
            //        FanTargetTemp = (double)jNode["FanTargetTemp"];
            //        FanDeadBand = (double)jNode["FanDeadBand"];
            //        FanLSHyst = (double)jNode["FanLSHyst"];
            //        FanHSHyst = (double)jNode["FanHSHyst"];
            //        FanLSVolts = (double)jNode["FanLSVolts"];
            //        FanHSVolts = (double)jNode["FanHSVolts"];

            //        jNode = jsonSysSettings["ControlValues"];
            //        RegulatorMode = (int)jNode["RegulatorMode"];
            //        FanMode = (int)jNode["FanMode"];
            //        RegulatorTargetTemp = (double)jNode["RegulatorTargetTemp"];
            //        SampleRate = (double)jNode["SampleRate"];

            //    }
            //    catch (Exception ex)
            //    {
            //        //myParentScript.ScriptLog(Severity.Error, "Parameter File Read Error: " + ex.ToString());
            //    }
            //}

        }

        public class Instrument
        {
            //public bool localAbort = false;
            List<string> creepResults = new List<string>();

            #region Instrument Instances
            DCmotor rotator;
            Sphincter verticalStage;
            Cavro fluidics;
            Laird tempBlock;
            Laird TEC2;
            Laird TEC1;
            panelControlerFFPE ffpeIns;
            #endregion

            #region Task Variables
            Task fluidicTask;
            Task tempTask;
            #endregion

            #region Script Params
            public int grinderVertOffset;
            #endregion

            #region Abort Variables
            public bool enzymeLoaded = false;
            public bool deliveryStarted = false;
            public bool deliveryFinished = false;
            public bool incStarted = false;
            public bool incFinished = false;
            public bool aborted = false;
            bool initialized = false;
            #endregion

            #region Maintenance Variables
            public string lineInput; //value can be Cells, Nuclei, All
            public string maintenanceTask; //value can be Rinse, Clean, 
            public bool zAxisPassed;
            public bool dcPassed;
            public bool fluidicsPassed;
            public bool coolTestFailed;
            public bool heatTestFailed;
            public List<int> failedLines = new List<int>();

            int clearlinestime;
            int clearlinestime2;
            int rinselinestime;
            int cleanlinestime;
            int stepperdiagtime = 634;
            int dcdiagtime = 204;
            int fluidicdiagtime = 135;
            int tempdiagtime = 1281;
            int calibrateTime = 16 * 60;

            #endregion

            #region Disruption Profiles
            int[] predisruptionZProfile = { 271000, 274000, 275000 };
            int[] nucPredisruptionZProfile = { 271000, 274000, 275000 };
            int[] nucPredisruptionRotProfile = { 1, 1, 1 };
            int[] predisruptionRotProfile = { 1, 1, 1 };
            int[] otherCellZProfile = { 262000, 267000, 270000, 272000, 274000, 272000, 275000, 276000 };
            int[] otherCellRotProfile = { 1, 1, 1, 2, 2, 2, 2, 2 };
            int[] lungZProfile = { 233000, 236000, 239000, 243000, 246000, 250000, 255000, 260000, 265000, 270000, 275000, 276000 };
            int[] lungRotProfile = { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };
            int[] nucZProfile = { 262000, 267000, 270000, 272000, 274000, 272000, 275000, 276000 };
            int[] nucRotProfile = { 2, 2, 2, 2, 2, 2, 2, 3 };
            int[] twoStepDisrupt = { 275000, 235000 }; //was 235000 then 275000
            int[] twoStepDisruptOne = { 275000, 246500 };
            int[] twoStepDisruptHalf = { 275000, 261250 };
            int[] twoStepMix = { 217000, 250000 }; //was 250000 then 217000
            int[] twoStepMixHalf = {261250, 269500}; //immersive for half 8250
            int[] twoStepMixOne = { 246500, 263000 }; //immersive for 1ml
            int[] twoStepWash = { 262000, 276000 };
            #endregion

            #region RunTime Variables
            public int incubateTime;
            public int setupTime = 10;
            public int initTime = 13;
            public int startTempTime = 1;
            public int engageAndPrimeTime = 21;
            public int deliverReagentOneTime;
            public int predisruptTime = 0;
            public int disruptAndPrimeTime; //need to find how long it takes if disrupt set to "none" //was 23
            public int strainTime = 42;
            public int washTime = 52; //was 76
            public int disengagingTime = 13; //was 18
            public int cleanupTime;
            public int totalRunTime;
            public int timeRemaining;
            #endregion

            #region Motor Logging Variables
            public string motorScriptName = "SinguScript2.4";
            public string motorFolderName = @"C:\ProgramData\LabScript\LogFiles\MotorLogs\";
            public string motorFileSuffix;
            public string motorFileName;
            public List<string> motorData = new List<string>();
            public int motorNum = 0;
            public bool newStep = true;
            #endregion

            int stepNum = 0;
            public DateTime startTime;
            public DateTime endTime;
            DateTime tempStartTime;
            double duration;
            double diff;
            double diffMs;
            short engageRPM = 95;
            short preDisruptionRPM = 75;
            int engageStep = 185000;
            bool constantVelocity = true;
            int expectedTime;
            public bool atTemp = false;
            public bool instrumentPresent = true;
            public bool comsFine = true;
            public string comError = "";
            public int comCounter;
            public string comErrorShort = "";
            public int startTime1;
            public short disSpeed;
            public short mixRPM;
            public short topMixRPM;


            private static string ScriptName = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            private static void ScriptLog(Severity mySeverity, string myLogMessage)
            {
                MAS.Framework.Logging.Lib.Logger.Log((MAS.Framework.Logging.Lib.Severity)mySeverity, myLogMessage, ScriptName);
            }

            private static OmegaScript myParentScript;

            //CStor
            public Instrument(OmegaScript myScript)
            {
                myParentScript = myScript;
                rotator = new DCmotor(myParentScript);
                verticalStage = new Sphincter(myParentScript);
                fluidics = new Cavro(myParentScript);
            }

            #region Universal Functions

            public int CalcInitTime()
            {
                int totalInitTime = setupTime + initTime;
                return totalInitTime;
            }
            public int CalcMaintTime(string task, string lines)
            {
                int maintTime;
                if (task == "Diagnostics")
                {
                    maintTime = 2460;
                }
                else if (task == "Calibration")
                {
                    maintTime = 1200;
                }
                else if (task == "Clean")
                {
                    if (lines == "Cells")
                    {
                        maintTime = 300;
                    }
                    else if (lines == "Nuclei")
                    {
                        maintTime = 500;
                    }
                    else //lines = All
                    {
                        maintTime = 810;
                    }
                }
                else //task = Rinse
                {
                    if (lines == "Cells")
                    {
                        maintTime = 80;
                    }
                    else if (lines == "Nuclei")
                    {
                        maintTime = 80;
                    }
                    else //lines = All
                    {
                        maintTime = 200;
                    }
                }
                return maintTime;
            }

            public int CalcRunTime()
            {

                #region Universal Times
                //incubate time, engage and prime time, strain time, wash time
                startTempTime = 1;
                engageAndPrimeTime = 21;
                deliverReagentOneTime = 47;
                strainTime = 42;
                washTime = 52;
                disengagingTime = 13;
                incubateTime = myParentScript.incTime * 60;

                #endregion

                #region Output Specific Times

                #region Cell Specific Calculations
                if (myParentScript.runOutput == "Cells")
                {
                    //predisruption time
                    if (myParentScript.predisruptChoice)
                    {
                        predisruptTime = 32;
                    }
                    //disruption time
                    if (myParentScript.disruptionStyle == "Cell_Default")
                    {
                        if (myParentScript.tissue == "Lung")
                        {
                            disruptAndPrimeTime = 203; //was 200
                        }
                        else
                        {
                            disruptAndPrimeTime = 112; //was 108 then 109
                        }
                    }
                    else if (myParentScript.disruptionStyle == "Cell_Triturate")
                    {
                        disruptAndPrimeTime = 56;
                        disruptAndPrimeTime = Convert.ToInt32(disruptAndPrimeTime * (20000 / myParentScript.vertSpeed));
                    }
                    else
                    {
                        disruptAndPrimeTime = 15;
                    }
                    //cleanup time
                    cleanupTime = 54;
                    //overall time
                    ////normal cell run
                    if (myParentScript.tissue != "Lung")
                    {
                        totalRunTime = startTempTime + engageAndPrimeTime + deliverReagentOneTime + predisruptTime + incubateTime + disruptAndPrimeTime + strainTime + (2 * (strainTime + washTime)) + disengagingTime + cleanupTime;
                    }
                    ////lung cell run
                    else
                    {
                        totalRunTime = startTempTime + engageAndPrimeTime + deliverReagentOneTime + predisruptTime + 2 * (incubateTime + disruptAndPrimeTime) + strainTime + (2 * (strainTime + washTime)) + disengagingTime + cleanupTime;
                    }
                }
                #endregion

                #region Nuclei Specific Calculations
                else
                {
                    if (myParentScript.disruptionStyle == "Nuclei_Default")
                    {
                        disruptAndPrimeTime = 132;
                    }
                    else if (myParentScript.disruptionStyle == "Nuclei_Dounce")
                    {
                        disruptAndPrimeTime = 55; //was 61
                        disruptAndPrimeTime = Convert.ToInt32(disruptAndPrimeTime * (20000 / myParentScript.vertSpeed));
                    }
                    else
                    {
                        disruptAndPrimeTime = 15;
                    }
                    //cleanup time
                    cleanupTime = 65;
                    //overall time
                    ////normal nuclei run
                    if (myParentScript.disruptCycles <= 1)
                    {
                        totalRunTime = startTempTime + engageAndPrimeTime + deliverReagentOneTime + predisruptTime + incubateTime + disruptAndPrimeTime + (2 * strainTime) + washTime + disengagingTime + cleanupTime;
                    }
                    ////extended nuclei run
                    else
                    {
                        totalRunTime = startTempTime + engageAndPrimeTime + deliverReagentOneTime + incubateTime + (2 * disruptAndPrimeTime) + (2 * strainTime) + washTime + disengagingTime + cleanupTime;
                    }
                }
                #endregion

                #endregion

                ////if (myParentScript.runOutput == "Cells" && myParentScript.thinLine == true)
                ////{
                ////    engageAndPrimeTime = 57;
                ////}
                ////else
                ////{
                ////    engageAndPrimeTime = 23;
                ////}

                //if (myParentScript.predisruptChoice)
                //{
                //    predisruptTime = 32;
                //}
                //else
                //{
                //    predisruptTime = 0;
                //}

                //if (myParentScript.disruptionStyle == "Cell_Default")
                //{
                //    if (myParentScript.tissue == "Lung")
                //    {
                //        disruptAndPrimeTime = 203; //was 200
                //    }
                //    else
                //    {
                //        disruptAndPrimeTime = 112; //was 108 then 109
                //    }
                //}
                //else if (myParentScript.disruptionStyle == "Cell_Triturate")
                //{
                //    disruptAndPrimeTime = 56; //was 54
                //    disruptAndPrimeTime = Convert.ToInt32(disruptAndPrimeTime * (20000 / myParentScript.vertSpeed));
                //}
                //else if (myParentScript.disruptionStyle == "Nuclei_Default")
                //{
                //    disruptAndPrimeTime = 132; //was 131
                //}
                //else if (myParentScript.disruptionStyle == "Nuclei_Dounce")
                //{
                //    disruptAndPrimeTime = 55; //was 61
                //    disruptAndPrimeTime = Convert.ToInt32(disruptAndPrimeTime * (20000/myParentScript.vertSpeed));
                //}
                //else
                //{
                //    disruptAndPrimeTime = 15;
                //}
                //if (myParentScript.disruptCycles == 2)
                //{
                //    disruptAndPrimeTime = 2 * disruptAndPrimeTime;
                //}
                //if (myParentScript.runOutput == "Cells")
                //{
                //    if (myParentScript.thinLine)
                //    {
                //        cleanupTime = 173; //was 128
                //        deliverReagentOneTime = 71; //was 105
                //    }
                //    else
                //    {
                //        cleanupTime = 54;
                //        deliverReagentOneTime = 47; //was 71
                //    }
                //}
                //else
                //{
                //    cleanupTime = 17;
                //    deliverReagentOneTime = 47; //was 71
                //}

                //if (myParentScript.runOutput == "Cells")
                //{
                //    if (myParentScript.tissue == "Lung")
                //    {
                //        totalRunTime = startTempTime + engageAndPrimeTime + deliverReagentOneTime + predisruptTime + 2 * (incubateTime + disruptAndPrimeTime) + strainTime + (2 * (strainTime + washTime)) + disengagingTime + cleanupTime;
                //    }
                //    else
                //    {
                //        totalRunTime = startTempTime + engageAndPrimeTime + deliverReagentOneTime + predisruptTime + incubateTime + disruptAndPrimeTime + strainTime + (2 * (strainTime + washTime)) + disengagingTime + cleanupTime;
                //    }
                //}
                //else
                //{
                //    //ScriptLog(Severity.Info, setupTime + "," + initTime + "," + startTempTime + "," + engageAndPrimeTime + "," + predisruptTime + "," + incubateTime + "," + disruptAndPrimeTime + "," + strainTime + "," + washTime + "," + disengagingTime + "," + cleanupTime);
                //    totalRunTime = startTempTime + engageAndPrimeTime + deliverReagentOneTime + predisruptTime + incubateTime + disruptAndPrimeTime + (2 * strainTime) + washTime + disengagingTime + cleanupTime;
                //}
                ScriptLog(Severity.Control, "totalRunTime = " + totalRunTime);

                timeRemaining = totalRunTime;

                return totalRunTime;
            }

            public void SetupOffset(int offset)
            {
                grinderVertOffset = offset;
                ScriptLog(Severity.Control, "Offset is " + grinderVertOffset);

                FactorOffset(twoStepMix);
                FactorOffset(twoStepMixHalf);
                FactorOffset(twoStepMixOne);
                FactorOffset(twoStepDisrupt);
                FactorOffset(twoStepDisruptOne);
                FactorOffset(twoStepDisruptHalf);
                FactorOffset(twoStepWash);
                FactorOffset(predisruptionZProfile);
                FactorOffset(otherCellZProfile);
                FactorOffset(lungZProfile);
                FactorOffset(nucZProfile);
                FactorOffset(nucPredisruptionZProfile);

            } //done

            public void ResetFlags()
            {
                stepNum = 0;

                enzymeLoaded = false;
                deliveryStarted = false;
                deliveryFinished = false;
                incStarted = false;
                incFinished = false;
                myParentScript.aborting = false;
                aborted = false;
                myParentScript.cannulaProb = false;

            } //done

            public void CheckCOMS()
            {
                comsFine = true;
                bool matched = false;
                comError = "";
                string[] guiDevices = { myParentScript.ComPortA, myParentScript.ComPortB, myParentScript.ComPortC, myParentScript.ComPortD };

                string[] ports = System.IO.Ports.SerialPort.GetPortNames();
                Thread.Sleep(1000);

                //check for issues with connecting to the instrument
                //0 com ports means the instrument isn't plugged into the tablet
                //wrong com ports means the tablet is plugged into a different instrument
                //less than 6 com ports means something is wrong with the instrument
                //could still have an issue even if all the com ports are present and correct

                comCounter = ports.Count<string>();
                //# of com ports will be 0 if the instrument is not connected
                if (comCounter == 0)
                {
                    comsFine = false;
                    comError = "The tablet does not appear to be connected to the instrument.\n ";
                    comErrorShort = "disconnected";
                }
                //this will probably never happen but if a usb gets unplugged internally or malfunctions we would see less than 6 devices
                else if (comCounter < 6)
                {
                    comsFine = false;
                    comError = "A device has been unplugged or is not working correctly.\nPlease reboot the instrument.";
                    comErrorShort = "malfunction";
                }

                //if everything is OK so far...
                if (comsFine)
                {
                    //checking whether com ports match commonscriptparameters, if they don't match we probably have a tablet/instrument mismatch
                    foreach (string device in guiDevices)
                    {
                        foreach (string com in ports)
                        {
                            if (device == com)
                            {
                                matched = true;
                            }
                        }
                        if (!matched)
                        {
                            //if no device matched a com port then we report it out and set comsFine to false so the rest of the functions don't run
                            ScriptLog(Severity.Control, $"Could not match {device}");
                            comError = "One or more COMS doesn't match. Check if tablet is on the correct instrument.";
                            comsFine = false;
                        }
                        else
                        {
                            //reset the matched variable
                            matched = false;
                        }

                    }
                }

                if (comError != "")
                {
                    ScriptLog(Severity.Control, comError);
                }
            }
            //but what do we do if the instrument is off and the COMs are present but we cannot connect to them...

            public void SetupCOMS(OmegaScript script)
            {
                //should we do this even if a COM port is missing? 
                //We don't technically need the RFID COM or the Chiller COM to have a successful run
                if (!comsFine && comCounter >= 6)
                {
                    ScriptLog(Severity.Info, "Trying to set up new COMs");
                    //tempBlock = new Laird(script);
                    TEC1 = new Laird(script);
                    TEC2 = new Laird(script);

                    SerialPort portOne = new SerialPort();
                    SerialPort portTwo = new SerialPort();
                    SerialPort portThree = new SerialPort();
                    SerialPort portFour = new SerialPort();
                    SerialPort portFive = new SerialPort();
                    SerialPort portSix = new SerialPort();
                    SerialPort[] devices = { portOne, portTwo, portThree, portFour, portFive, portSix };

                    //get list of ports
                    string[] ports = SerialPort.GetPortNames();
                    ScriptLog(Severity.Control, "Full COM List: " + string.Join(" ", ports));

                    //separate ports into serial and usb ports
                    string[] properties = { "dwSettableBaud" };
                    List<string> serialPortsList = new List<string>();
                    List<string> usbPortsList = new List<string>();
                    int n = 0;
                    foreach (string port in ports)
                    {
                        devices[n] = new SerialPort(port);
                        devices[n].Open();
                        object p = devices[n].BaseStream.GetType().GetField("commProp", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(devices[n].BaseStream);
                        Int32 baud = (Int32)p.GetType().GetField("dwSettableBaud", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(p);
                        if (baud > 500000)
                        {
                            serialPortsList.Add(port);
                        }
                        else
                        {
                            usbPortsList.Add(port);
                        }
                        //ScriptLog(Severity.Info, baud.ToString());
                        //ScriptLog(Severity.Info, baudMax.ToString());
                        //ScriptLog(Severity.Info, parameters.ToString());
                        //ScriptLog(Severity.Info, subType.ToString());
                        devices[n].Close();
                        n++;
                    }
                    ScriptLog(Severity.Control, "Serial Port List: " + string.Join(" ", serialPortsList.ToArray()));
                    ScriptLog(Severity.Control, "USB Port List: " + string.Join(" ", usbPortsList.ToArray()));

                    #region Assign USB Ports

                    #region Stepper
                    string stepperPort = verticalStage.Connect(usbPortsList);

                    bool removed = usbPortsList.Remove(stepperPort);

                    ScriptLog(Severity.Control, "Vertical Motor Power Reading = " + verticalStage.GetADCReading());


                    ScriptLog(Severity.Control, "Vertical = " + stepperPort);
                    ScriptLog(Severity.Control, "Successfully removed port from list? " + removed);

                    if (verticalStage.GetADCReading() <= 0)
                    {
                        comErrorShort = "off";
                    }

                    #endregion

                    #region RFID

                    string rfidPort;

                    if (usbPortsList.Count() == 1)
                    {
                        rfidPort = usbPortsList[0];
                    }
                    else
                    {
                        rfidPort = "UNKNOWN";
                    }

                    ScriptLog(Severity.Control, "RFID Reader = " + rfidPort);

                    #endregion

                    #endregion

                    if (comErrorShort != "off")
                    {

                        #region Assign Serial Ports

                        #region Temp
                        ScriptLog(Severity.Info, "Setting Up Temp...");

                        //foreach (string device in portsList)
                        //{
                        //    ScriptLog(Severity.Info, "Closing " + device);
                        //    SerialPort serPort = new SerialPort(device);
                        //    serPort.Dispose();
                        //    serPort.Close();
                        //    ScriptLog(Severity.Info, device + " closed.");
                        //}
                        string LairdPort1 = "";
                        string LairdPort2 = "";
                        LairdPort1 = TEC1.Connect(serialPortsList);
                        //MessageBox.Show("Laird Port: " + LairdPort1);
                        if (LairdPort1 != "")
                        {
                            //bool IsConnected = tempBlock.ModSetup(LairdPort1);
                            //MessageBox.Show("Connected to Laird? " + IsConnected.ToString());
                            TEC1.Setup(LairdPort1);
                            double temp = -999;
                            for (int f = 0; f < 2; f++)
                            {
                                temp = TEC1.GetTemperature(2); // 2: only on internal
                                                               //MessageBox.Show("Temp: " + temp.ToString());
                                if (temp > -900)
                                    break;
                            }
                            ScriptLog(Severity.Info, "Reading = " + temp);

                            if (temp < -900)
                            {
                                LairdPort2 = LairdPort1;
                                ScriptLog(Severity.Info, "Chiller = " + LairdPort2);
                                serialPortsList.Remove(LairdPort2);
                                TEC1.Cleanup();
                                LairdPort1 = TEC2.Connect(serialPortsList);
                                ScriptLog(Severity.Info, "Internal Temp = " + LairdPort1);
                            }
                            else
                            {
                                ScriptLog(Severity.Info, "Internal Temp = " + LairdPort1);
                                ScriptLog(Severity.Info, "Removing Internal LAIRD from List");
                                serialPortsList.Remove(LairdPort1);
                                ScriptLog(Severity.Info, "New List: " + string.Join(" ", serialPortsList.ToArray()));
                                //tempBlock.Cleanup();
                                ScriptLog(Severity.Info, "Disconnected from Internal LAIRD");
                                LairdPort2 = TEC2.Connect(serialPortsList);
                                ScriptLog(Severity.Info, "Chiller = " + LairdPort2);
                            }
                            //MessageBox.Show("Chiller = " + LairdPort2);

                        }
                        else
                        {
                            ScriptLog(Severity.Info, "Internal TEC Off or Disconnected.");
                        }
                        #endregion

                        #region Temp 2
                        ////ScriptLog(Severity.Info, "Setting Up Temp 2...");
                        //Laird myLaird2 = new Laird(this);
                        //string LairdPort2 = myLaird2.Connect(portsList);

                        //if (LairdPort2 != "")
                        //{
                        //    if (myLaird2.GetTemperature() < -900)
                        //    {
                        //        LairdPort2 = "";
                        //    }
                        //    else
                        //    {
                        //        //ScriptLog(Severity.Info, "Reading = " + myLaird2.GetTemperature());
                        //        ScriptLog(Severity.Info, "Temp2 = " + LairdPort2);
                        //        portsList.Remove(LairdPort2);
                        //    }
                        //}
                        //else
                        //{
                        //    ScriptLog(Severity.Info, "Chiller Off or Disconnected.");
                        //}
                        #endregion

                        #region DC Motor
                        ScriptLog(Severity.Info, "Setting Up Rotation...");
                        string RoboClawPort = rotator.Connect(serialPortsList);
                        serialPortsList.Remove(RoboClawPort);
                        ScriptLog(Severity.Control, "Rotation = " + RoboClawPort);
                        #endregion

                        #region Fluidics
                        //ScriptLog(Severity.Info, "Setting Up Fluidics...");
                        string CavroPort = fluidics.Connect(serialPortsList);

                        ScriptLog(Severity.Info, "Fluidics = " + CavroPort);
                        //MessageBox.Show("Fluidics = " + CavroPort);
                        serialPortsList.Remove(CavroPort);
                        #endregion

                        #endregion

                        //record ports
                        string[] coms = { CavroPort, RoboClawPort, stepperPort, LairdPort1, LairdPort2, rfidPort };
                        string[] comsThatMatter = { CavroPort, RoboClawPort, stepperPort, LairdPort1 };
                        comsFine = true;
                        foreach (string comPort in comsThatMatter)
                        {
                            //MessageBox.Show("Found: " + comPort);

                            if (!comPort.Contains("COM"))
                            {
                                comsFine = false;
                                comErrorShort = "malfunction";
                            }
                        }
                        myParentScript.SaveCOMs(coms);
                        verticalStage.Cleanup();
                        rotator.Cleanup();
                        TEC1.Cleanup();
                        fluidics.Discon();
                    }

                }
            }

            public void Setup(OmegaScript script)
            {
                if (comsFine)
                {
                    int deviceCheck = 0;
                    //startTime = DateTime.UtcNow;
                    tempBlock = new Laird(script);
                    ScriptLog(Severity.Info, "Connecting...");
                    ScriptLog(Severity.Control, "Setting Up..." + DateTime.UtcNow);
                    ScriptLog(Severity.Control, "COM Ports are: " + myParentScript.ComPortA + ", " + myParentScript.ComPortB + ", " + myParentScript.ComPortC + ", " + myParentScript.ComPortD);
                    //check whether the devices are powered on when setting up, if not we want to skip initialize so comsFine gets set to false
                    bool rotConnected = rotator.Setup(myParentScript.ComPortB);
                    ScriptLog(Severity.Control, "Rotator Connected = " + rotConnected);
                    if (!rotConnected)
                    {
                        ScriptLog(Severity.Control, "Cannot communicate with rotator.");
                        comsFine = false;
                        deviceCheck++;
                    }
                    bool vertConnected = verticalStage.SetupWithParams(myParentScript.ComPortC);
                    ScriptLog(Severity.Control, "Vert Connected = " + vertConnected);
                    if (!vertConnected)
                    {
                        ScriptLog(Severity.Control, "Cannot communicate with vertical stage.");
                        comsFine = false;
                        deviceCheck++;
                    }
                    bool fluidicConnected = fluidics.Setup(myParentScript.ComPortA);
                    ScriptLog(Severity.Control, "Fluidic Connected = " + fluidicConnected);
                    if (!fluidicConnected)
                    {
                        ScriptLog(Severity.Control, "Cannot communicate with fluidics assembly.");
                        comsFine = false;
                        deviceCheck++;
                    }
                    bool tempOK = tempBlock.SoftSetup(myParentScript.ComPortD);
                    bool tempConnected = false;
                    if (tempOK == true)
                    {
                        tempConnected = tempBlock.Setup(myParentScript.ComPortD);
                    }
                    ScriptLog(Severity.Control, "Temp Connected = " + tempConnected);
                    if (!tempConnected)
                    {
                        ScriptLog(Severity.Control, "Cannot communicate with temperature controller.");
                        comsFine = false;
                        deviceCheck++;
                    }
                    if (deviceCheck < 4)
                    {
                        comErrorShort = "malfunction";
                    }
                    else if (deviceCheck == 4)
                    {
                        comErrorShort = "off";
                    }
                    //endTime = DateTime.UtcNow;
                    //ExtendRunTime(setupTime);
                }

            } //done, Abort done (should re-check process though)

            public void Init()
            {
                if (comsFine)
                {
                    stepNum++;
                    //startTime = DateTime.UtcNow;
                    ScriptLog(Severity.Info, "Initializing...");
                    ScriptLog(Severity.Control, "Initializing..." + DateTime.UtcNow);
                    ScriptLog(Severity.Control, "Stepper");
                    verticalStage.Initialize();
                    ScriptLog(Severity.Control, "Fluidics");
                    //fluidics.InitializeThreeDevices();
                    fluidics.Initialize(2);
                    //endTime = DateTime.UtcNow;
                    //ExtendRunTime(initTime);
                    initialized = true;
                }
                else
                {
                    if (comErrorShort == "malfunction")
                    {
                        //FIXTHIS
                        myParentScript.cusMsgBox2.Show("One or more devices aren't working.\n ", "Warning", MessageBoxButtons.OK);
                        //MessageBox.Show("One or more devices aren't working.\n ", "Warning", MessageBoxButtons.OK);
                    }
                    else if (comErrorShort == "off")
                    {
                        //FIXTHIS
                        myParentScript.cusMsgBox2.Show("Instrument is off.\n ", "Warning", MessageBoxButtons.OK);
                        //MessageBox.Show("Instrument is off.\n ", "Warning", MessageBoxButtons.OK);
                    }
                    else
                    {
                        //FIXTHIS
                        myParentScript.cusMsgBox2.Show("Instrument disconnected.\n ", "Warning", MessageBoxButtons.OK);
                        //MessageBox.Show("Instrument disconnected.\n ", "Warning", MessageBoxButtons.OK);
                    }
                }
            } //need to change to check, Abort done

            public void EngageCartridge()
            {
                stepNum++;
                ScriptLog(Severity.Info, "Engaging Cartridge...");
                ScriptLog(Severity.Control, "| Engaging Cartridge..." + DateTime.UtcNow);
                //verticalStage.MoveToWaitDone(engageStep + grinderVertOffset); //could just store this value somewhere
                verticalStage.MoveCheckInterlock(engageStep + grinderVertOffset); //could just store this value somewhere
                rotator.RotateOnce(2000, engageRPM, false); //could create a rotator function called rotator.EngageCartridge();
                //verticalStage.MoveToWaitDone(engageStep + grinderVertOffset - 20000); //UNNECESSARY STEP
                ScriptLog(Severity.Control, $"| Engagged Cartridge... {DateTime.UtcNow}");
            } //done, Abort done

            public void Strain()
            {
                stepNum++;
                startTime = DateTime.UtcNow;
                ScriptLog(Severity.Info, "Straining Sample...");
                ScriptLog(Severity.Control, "Straining Sample..." + DateTime.UtcNow);
                for (int cycle = 0; cycle < 1 && !myParentScript.abortRequested; cycle++)
                {
                    fluidics.StrainSample(200);
                }
                endTime = DateTime.UtcNow;
                ExtendRunTime(strainTime);
            } //done

            public void DisengageCartridge()
            {
                stepNum++;
                startTime = DateTime.UtcNow;
                ScriptLog(Severity.Info, "Disengaging Cartridge...");
                ScriptLog(Severity.Control, "| Disengaging Cartridge..." + DateTime.UtcNow);
                //verticalStage.MoveToWaitDone(0);
                verticalStage.Initialize();
                endTime = DateTime.UtcNow;
                ExtendRunTime(disengagingTime);
            } //done

            #endregion

            #region Universal Functions with Diff Inputs

            public void Deliver()
            {
                int amount;

                
                /*
                if (myParentScript.disruptVol > 0.0)
                {
                    amount = Convert.ToInt32(600 * myParentScript.disruptVol);
                }
                else
                {
                    amount = 1200;
                }
                stepNum++;
                startTime = DateTime.UtcNow;
                //ScriptLog(Severity.Info, "Delivering Disruption Reagent..." + DateTime.UtcNow);
                if (!myParentScript.abortRequested)
                {
                    deliveryStarted = true;
                }
                if (myParentScript.runOutput == "Cells")
                {
                    ScriptLog(Severity.Control, "Delivering Enzyme Solution..." + DateTime.UtcNow);
                    fluidics.DeliverEnz(amount);
                }
                else if (myParentScript.rSource == "SingleShot")
                {
                    ScriptLog(Severity.Control, "Delivering Nuclei Isolation Reagent from Single Shot..." + DateTime.UtcNow);
                    fluidics.DeliverEnz(amount);
                }
                else if (myParentScript.rSource == "Manual")
                {
                    ScriptLog(Severity.Control, "User already added reagent to cartridge");
                }
                else
                {
                    ScriptLog(Severity.Control, "Delivering Nuclei Isolation Reagent..." + DateTime.UtcNow);
                    fluidics.DeliverNucIso(amount);
                }
                if (!myParentScript.abortRequested)
                {
                    deliveryFinished = true;
                }

                */
                endTime = DateTime.UtcNow;
                ExtendRunTime(deliverReagentOneTime);
            } //done

            public void explorerDelivery(int valvePos, int solutionDeliver, int airPrime, int pumpSPd)
            {
                //ScriptLog(Severity.Control, "  Priming Solution...");
                //fluidics.Prime(valvePos);
                string SSPosition;
                if (valvePos == 10)
                {
                    SSPosition = "Buffer(Left) Straw";
                }
                else
                {
                    SSPosition = "Enzyme(Right) Straw";
                }                
                //ScriptLog(Severity.Control, "|- Delivering Solution...");
                ScriptLog(Severity.Control, $"|-- Delivering from {SSPosition}");
                ScriptLog(Severity.Control, $"|-- Pump Speed: {pumpSPd}");
                fluidics.flexDeliver(solutionDeliver, airPrime, valvePos, pumpSPd);
                ScriptLog(Severity.Control, "|-- Clearing Line");
                fluidics.ClearLines();
                //ScriptLog(Severity.Control, "|- Delivered Solution...");

            }

            public void explorerStrain(int strainSteps, int check, int which, int speed)
            {
                ScriptLog(Severity.Control, "|- Straining Solution...");
                if (check == 40 && which == 1) //straining 0.5ml when 1ml is delivered
                {
                    ScriptLog(Severity.Control, "|-- Straining 0.5ml of Solution...");
                    verticalStage.MoveToWaitDone(265750 + grinderVertOffset);
                }
                else if (check == 40 && which == 2) //straining 1ml when 2ml is delivered
                {
                    ScriptLog(Severity.Control, "|-- Straining 1ml of Solution...");
                    verticalStage.MoveToWaitDone(255500 + grinderVertOffset);
                }
                else
                {
                    ScriptLog(Severity.Control, "|-- Straining All of the Solution...");
                    verticalStage.MoveToWaitDone(276000 + grinderVertOffset);
                }
                fluidics.flexStrain(strainSteps, speed);
                ScriptLog(Severity.Control, "|- Strained Solution...");
            }

            public void Wash()
            {
                stepNum++;
                startTime = DateTime.UtcNow;
                ScriptLog(Severity.Info, "Rinsing Sample...");
                ScriptLog(Severity.Control, "Rinsing Sample..." + DateTime.UtcNow);
                bool rotate = true;

                #region Wash Chamber
                deliveryFinished = false;
                if (myParentScript.runOutput == "Cells")
                {
                    fluidics.DeliverBuffer();
                }
                else if (myParentScript.rSource == "SingleShot" || myParentScript.rSource == "Manual")
                {
                    fluidics.DeliverBuffer();
                }
                else
                {
                    fluidics.DeliverNucStor();
                }
                deliveryFinished = true;
                #endregion

                ScriptLog(Severity.Control, "Washing Disrupter Head Started");

                #region Wash Disrupter Head
                if (rotate)
                {
                    rotator.SetMotorRPM(135, "forward");
                }
                for (int i = 0; i < 6 && !myParentScript.abortRequested; i++)
                {
                    //verticalStage.MoveToWaitDone(262000 + off);
                    //verticalStage.MoveToWaitDone(276000 + off);
                    verticalStage.MoveToMultiSteps(twoStepWash, 0);
                }
                rotator.MotorStop();
                #endregion

                ScriptLog(Severity.Control, "Washing Disrupter Head Finished");

                endTime = DateTime.UtcNow;
                ExtendRunTime(washTime);
            } //done

            public void PredisruptSample()
            {
                //if (myParentScript.predisruptChoice)
                //{
                //if (myParentScript.runOutput == "Cells")
                //{
                stepNum++;
                startTime = DateTime.UtcNow;
                ScriptLog(Severity.Info, "Predisrupting Sample...");
                ScriptLog(Severity.Control, "| Predisrupting Sample..." + DateTime.UtcNow);
                for (int i = 0; i < predisruptionZProfile.Count() && !myParentScript.abortRequested; i++)
                {
                    #region Vertical Move
                    verticalStage.MoveStep(predisruptionZProfile[i]);
                    #endregion

                    #region Rotate
                    if (constantVelocity)
                    {
                        rotator.RotateConstVel(predisruptionRotProfile[i], 4000, preDisruptionRPM, true);
                    }
                    else
                    {
                        rotator.Rotate(predisruptionRotProfile[i], 4000, preDisruptionRPM, true);
                    }
                    #endregion
                }

                endTime = DateTime.UtcNow;
                ExtendRunTime(predisruptTime);
                ScriptLog(Severity.Control, "| Predisrupted Sample...");
                //}
                //else
                //{

                //}
            } //done

            public void Incubate()
            {
                //ScriptLog(Severity.Info, "Incubation Time = " + myParentScript.incTime);
                string incubateType = myParentScript.mixingStyle;
                int time = myParentScript.incTime;
                ScriptLog(Severity.Control, "Incubation Time = " + time);
                short mixingRPM = Convert.ToInt16(myParentScript.mixingSpeed);
                incStarted = true;
                incFinished = false;
                if (time != 0)
                {
                    stepNum++;
                    startTime = DateTime.UtcNow;
                    ScriptLog(Severity.Info, "Incubating Sample...");
                    ScriptLog(Severity.Control, "Incubating Sample..." + DateTime.UtcNow);

                    int i = 0;
                    DateTime start = DateTime.UtcNow;

                    verticalStage.MoveToWaitDone(235000 + grinderVertOffset);
                    if (myParentScript.disruptVol == 1.0)
                    {
                        verticalStage.MoveToWaitDone(255500 + grinderVertOffset);
                    }
                    else if (myParentScript.disruptVol == 0.5)
                    {
                        verticalStage.MoveToWaitDone(265750 + grinderVertOffset);
                    }

                    while (DateTime.UtcNow - start < TimeSpan.FromMinutes(time) && !myParentScript.abortRequested) //incubates for minute value placed in the input box
                    {
                        if (incubateType == "Immersive")
                        {
                            //MixImmersive();
                        }

                        else if (incubateType == "Triturate")
                        {
                            //MixTrit(i);
                            i++;
                        }

                        else if (incubateType == "Top")
                        {
                            //MixTop(); //NOTE: this will take input from user instead of automatically setting mixing RPM to 150 like we used to
                        }

                        else
                        {
                            rotator.Sleep(1000);
                        }
                    }
                    //incFinished = true;
                    endTime = DateTime.UtcNow;
                    ExtendRunTime(time * 60);
                }
            } //done

            public void incubationExplorer(int check, int time, int speed, int which) 
            {
                //ffpeIns.myTest = check;
                //int time = ffpeIns.incubationTime;
                //ScriptLog(Severity.Control, "Incubation Time = " + time);
                //ScriptLog(Severity.Control, "Speed of RoboClaw = " + speed);
                int[] immPattern;
                int[] titPattern;

                if (time != 0)
                {
                    //stepNum++;
                    //startTime = DateTime.UtcNow;
                    ScriptLog(Severity.Info, "Incubating Sample...");
                    ScriptLog(Severity.Control, "| Incubating Sample..." + DateTime.UtcNow);
                    ScriptLog(Severity.Control, $"|- Incubation Time Set to {time} Mins");
                    ScriptLog(Severity.Control, $"|- Mixing Speed set to {speed} Rpm");

                    int i = 0;
                    DateTime start = DateTime.UtcNow;

                    ScriptLog(Severity.Control, "|- Moving stage down " + DateTime.UtcNow);
                    ScriptLog(Severity.Control, "|-- At position:" + verticalStage.GetPosition());
                    
                    //verticalStage.MoveToWaitDone(235000 + grinderVertOffset);
                    //int[] twoStepMix = { 217000, 250000 };
                    //0.5 = 
                    //1.0 = {237500, 270500}
                    //++++++++BRING BACK +++++++
                    if (which == 1) //1ml
                    {
                        ScriptLog(Severity.Control, "|- Reached desired position:" + (255500 + grinderVertOffset));
                        verticalStage.MoveToWaitDone(255500 + grinderVertOffset);
                        ScriptLog(Severity.Control, "|- Reached desired position:" + (255500 + grinderVertOffset));
                        immPattern = twoStepMixOne;
                        titPattern = twoStepDisruptOne;

                    }
                    else if (which == 3) //0.5
                    {
                        ScriptLog(Severity.Control, "|- Reached desired position:" + (265750 + grinderVertOffset));
                        verticalStage.MoveToWaitDone(265750 + grinderVertOffset); //189130
                        ScriptLog(Severity.Control, "|- Reached desired position:" + (265750 + grinderVertOffset));
                        immPattern = twoStepMixHalf;
                        titPattern = twoStepDisruptHalf;
                    }
                    else
                    {
                        ScriptLog(Severity.Control, "|-- Going to position:" + (235000 + grinderVertOffset));
                        verticalStage.MoveToWaitDone(235000 + grinderVertOffset);
                        ScriptLog(Severity.Control, "|- Reached desired position:" + (235000 + grinderVertOffset));
                        immPattern = twoStepMix;
                        titPattern = twoStepDisrupt;
                    }
                    

                    if (check == 10)
                    {
                        ScriptLog(Severity.Control, "|- Preformming Top Mix " + DateTime.UtcNow);

                    }
                    else if (check == 11)
                    {
                        ScriptLog(Severity.Control, "|- Preformming Immersive Mix " + DateTime.UtcNow);
                    }
                    
                    while (DateTime.UtcNow - start < TimeSpan.FromMinutes(time)) // && !myParentScript.abortRequested) //incubates for minute value placed in the input box
                    {
                        if (check == 10)
                        {
                            MixTop(speed);
                            
                        }
                        else if (check == 11)
                        {
                            MixImmersive(speed, immPattern);
                        }
                        else if (check == 14)
                        {
                            MixTrit(i, speed, titPattern);
                            i++;
                        }
                    }


                }
            }

            public void Disrupt()
            {
                string disruptType = myParentScript.disruptionStyle;
                ScriptLog(Severity.Control, "Disruption Style = " + disruptType);
                string tissueType = myParentScript.tissue;
                short disruptSpeed = Convert.ToInt16(myParentScript.disruptionSpeed);
                ScriptLog(Severity.Control, "Disruption Speed = " + disruptSpeed);
                if (disruptType != "None")
                {
                    stepNum++;
                    ScriptLog(Severity.Info, "Disrupting Sample...");
                    ScriptLog(Severity.Control, "Disrupting Sample..." + DateTime.UtcNow);
                    if (disruptType == "Cell_Default")
                    {
                        //CellDefaultDisrupt();
                    }

                    else if (disruptType == "Cell_Triturate")
                    {
                        //CellTritDisrupt();
                    }

                    else if (disruptType == "Nuclei_Default")
                    {
                        //NucleiDefaultDisrupt();
                    }

                    else if (disruptType == "Nuclei_Dounce")
                    {
                        //NucleiDounceDisrupt();
                    }
                }
                else
                {
                    verticalStage.MoveToWaitDone(276000 + grinderVertOffset);
                }
            } //done

            public void EndRun()
            {
                string runType = myParentScript.runOutput;
                stepNum++;
                startTime = DateTime.UtcNow;
                ScriptLog(Severity.Info, "Completing Run...");
                ScriptLog(Severity.Control, "Completing Run..." + DateTime.UtcNow);
                myParentScript.abortRequested = false;
                if ((runType == "Cells" || myParentScript.rSource == "SingleShot") && enzymeLoaded)
                {
                    fluidics.CleanupCellRun();
                }
                else
                {
                    fluidics.Cleanup();
                }
                //verticalStage.Cleanup();
                rotator.MotorStop();
                tempBlock.Stop();

                endTime = DateTime.UtcNow;
                ExtendRunTime(cleanupTime);
            } //done

            public void explorerEndRun()
            {
                rotator.MotorStop();
            }

            #endregion

            #region Disrupt Functions

            public void CellDefaultDisrupt(int speed, string tissue)
            {
                //string tissue = myParentScript.tissue;
                //short cellRPM = Convert.ToInt16(myParentScript.disruptionSpeed);
                List<int> disruptionProfile = new List<int>(otherCellZProfile);
                List<int> rotationProfile = new List<int>(otherCellRotProfile);

                //
                //ScriptLog(Severity.Control, "Set variables and entering for loop ");
                //ScriptLog(Severity.Control, "disruptionProfile: " + disruptionProfile);
                //ScriptLog(Severity.Control, "rotationProfile: " + rotationProfile);
                
                disSpeed = Convert.ToInt16(speed);
                //ScriptLog(Severity.Control, "disSpeed: " + disSpeed);

                if (tissue == "Lung")
                {
                    disruptionProfile = new List<int>(lungZProfile);
                    rotationProfile = new List<int>(lungRotProfile);
                }
                for (int n = 0; n < disruptionProfile.Count() && !myParentScript.abortRequested; n++)
                {
                    #region Vertical Move
                    int myStep = verticalStage.MoveStep(disruptionProfile[n]);
                    #endregion

                    #region Rotate
                    if (constantVelocity)
                    {
                        rotator.RotateConstVel(rotationProfile[n], 4000, disSpeed, true); //disSped
                    }
                    else
                    {
                        rotator.Rotate(rotationProfile[n], 4000, disSpeed, true); //disSped
                    }
                    #endregion
                }
                
            } //done

            public void CellTritDisrupt(int speed)
            {
                int totalTriturations = 10;
                disSpeed = Convert.ToInt16(speed);
                //ScriptLog(Severity.Control, "Setting up vertSTage speed: " + myParentScript.vertSpeed);
                verticalStage.SetMaxSpeed(20000); // myParentScript.vertSpeed);
                //ScriptLog(Severity.Control, "Entering For Loop w/ totTrituration " + totalTriturations);               
                //ScriptLog(Severity.Control, "disSpeed: " + disSpeed);
                for (int i = 1; i <= totalTriturations && !myParentScript.abortRequested; i++)
                {
                    //verticalStage.MoveToWaitDone(235000 + off);
                    //verticalStage.MoveToWaitDone(275000 + off);
                    verticalStage.MoveToMultiSteps(twoStepDisrupt, 0);
                    rotator.SetMotorRPM(disSpeed, "forward");
                    Thread.Sleep(250);
                    rotator.MotorStop();
                }
                verticalStage.SetMaxSpeed(20000);
                verticalStage.MoveToWaitDone(276000 + grinderVertOffset);
            } //done

            public void NucleiDefaultDisrupt(int speed)
            {
                short nucRPM = Convert.ToInt16(speed);
                List<int> disruptionProfile = new List<int>(nucZProfile);
                List<int> rotationProfile = new List<int>(nucRotProfile);

                for (int n = 0; n < disruptionProfile.Count() && !myParentScript.abortRequested; n++)
                {
                    #region Vertical Move
                    int myStep = verticalStage.MoveStep(disruptionProfile[n]);
                    #endregion

                    #region Rotate
                    if (constantVelocity)
                    {
                        rotator.RotateConstVel(rotationProfile[n], 4000, nucRPM, true);
                    }
                    else
                    {
                        rotator.Rotate(rotationProfile[n], 4000, nucRPM, true);
                    }
                    #endregion
                }
            } //done

            public void customeDisruptProfile(int[] vertArr, int[] rotoArr, int speed)
            {
                ScriptLog(Severity.Control, $"|- Rotaion speed: {speed} rpm ");
                short cusRPM = Convert.ToInt16(speed);
                List<int> disruptionProfile = new List<int>(vertArr);
                List<int> rotationProfile = new List<int>(rotoArr);

                for (int n = 0; n < disruptionProfile.Count() && !myParentScript.abortRequested; n++)
                {
                    #region Vertical Move
                    ScriptLog(Severity.Control, $"|-- Stepper Step {n + 1}: {disruptionProfile[n] - grinderVertOffset} ");
                    int myStep = verticalStage.MoveStep(disruptionProfile[n]);
                    #endregion

                    #region Rotate
                    ScriptLog(Severity.Control, $"|--- Rotation Step {n + 1}: {rotationProfile[n]} ");
                    if (constantVelocity)
                    {
                        rotator.RotateConstVel(rotationProfile[n], 4000, cusRPM, true);
                    }
                    else
                    {
                        rotator.Rotate(rotationProfile[n], 4000, cusRPM, true);
                    }
                    #endregion
                }
            }

            public void NucleiDounceDisrupt(int speed, int which)
            {
                int[] dounce;
                ;
                if (which == 1) //1ml
                {
                    verticalStage.MoveToWaitDone(255500 + grinderVertOffset);
                    //immPattern = twoStepMixOne;
                    dounce = twoStepDisruptOne;

                }
                else if (which == 3) //0.5
                {
                    verticalStage.MoveToWaitDone(265750 + grinderVertOffset); //189130
                    //immPattern = twoStepMixHalf;
                    dounce = twoStepDisruptHalf;
                }
                else
                {
                    
                    verticalStage.MoveToWaitDone(235000 + grinderVertOffset);
                    //immPattern = twoStepMix;
                    dounce = twoStepDisrupt;
                }

                
                int totalDounces = 10;
                verticalStage.SetMaxSpeed(speed * 1000); //Convert.ToInt32(speed)
                ScriptLog(Severity.Control, $"|- Set Stepper Speed to {speed * 1000}");
                
                for (int i = 1; i <= totalDounces && !myParentScript.abortRequested; i++)
                {
                    //verticalStage.MoveToWaitDone(235000 + off);
                    //verticalStage.MoveToWaitDone(275000 + off);
                    verticalStage.MoveToMultiSteps(dounce, 0);
                    rotator.SetMotorRPM(50, "forward");
                    Thread.Sleep(250);
                    rotator.MotorStop();
                }
                
                verticalStage.SetMaxSpeed(20000);
                verticalStage.MoveToWaitDone(276000 + grinderVertOffset);
            } //done

            #endregion

            #region Incubate Functions

            public void MixImmersive(int speed, int[] pattern)
            {
                mixRPM = Convert.ToInt16(speed);
                //ScriptLog(Severity.Control, "Mix Rpm set to " + mixRPM);
                rotator.SetMotorRPM(mixRPM, "forward");
                //ScriptLog(Severity.Control, $"|-- {Convert.ToInt32(pattern)}");
                //verticalStage.MoveToWaitDone(250000 + off);
                //verticalStage.MoveToWaitDone(217000 + off);
                verticalStage.MoveToMultiSteps(pattern, 0);
                rotator.MotorStop();
            } //done

            public void MixTrit(int j, int speed, int[] pattern)
            {
                mixRPM = Convert.ToInt16(speed); 

                if (j % 30 != 0)
                {
                    verticalStage.Sleep(1000);
                }

                else
                {
                    for (int i = 0; i < 10; i++)
                    {
                        //verticalStage.MoveToWaitDone(235000 + off);
                        //verticalStage.MoveToWaitDone(275000 + off);
                        verticalStage.MoveToMultiSteps(pattern, 0);
                        rotator.SetMotorRPM(mixRPM, "forward");
                        rotator.Sleep(250);
                        rotator.MotorStop();
                    }
                }
            } //done

            public void MixTop(int speed)
            {
                //ScriptLog(Severity.Control, "|- Preformming Top Mix " + DateTime.UtcNow);
                topMixRPM = Convert.ToInt16(speed);//myParentScript.mixingSpeed);
                //ScriptLog(Severity.Control, "topMixRPM: " + topMixRPM);
                rotator.SetMotorRPM(topMixRPM, "forward");
                rotator.Sleep(1000);
                rotator.MotorStop();

            } //done

            #endregion

            #region Asynchronous Functions

            public void WaitTempAsync()
            {
                ScriptLog(Severity.Info, "Waiting for Temp...");
                ScriptLog(Severity.Control, "Waiting for Temp..." + DateTime.UtcNow);
                tempTask.Wait();
            } //done

            public void WaitFluidicAndEngageAsync()
            {
                fluidicTask.Wait();
                endTime = DateTime.UtcNow;
                ExtendRunTime(engageAndPrimeTime);
            } //done

            public void WaitFluidicAndDisruptAsync()
            {
                fluidicTask.Wait();
                endTime = DateTime.UtcNow;
                ExtendRunTime(disruptAndPrimeTime);
            } //done

            public async void PrimePrimary()
            {
                string runType = myParentScript.runOutput;
                startTime = DateTime.UtcNow;
                ScriptLog(Severity.Control, "Priming...");
                fluidicTask = new Task(() => fluidics.PrimeNucIsoandClearLines());
                if (runType == "Cells" || myParentScript.rSource == "SingleShot")
                {
                    fluidicTask = new Task(() => fluidics.PrimeEnzandClearLines());
                    //enzymeLoaded = true;
                }
                else if (myParentScript.rSource == "Manual")
                {
                    ScriptLog(Severity.Control, "Priming skipped for Manual Delivery");
                    fluidicTask = new Task(() => fluidics.NewMoveToValve(6));
                }
                fluidicTask.Start();
                await fluidicTask;
            } //done

            public async void PrimeSecondary()
            {
                string runType = myParentScript.runOutput;
                startTime = DateTime.UtcNow;
                ScriptLog(Severity.Control, "Priming Rinse Reagent...");
                fluidicTask = new Task(() => fluidics.PrimeNucStorandClearLines());
                if (runType == "Cells" || myParentScript.rSource == "SingleShot" || myParentScript.rSource == "Manual")
                {
                    fluidicTask = new Task(() => fluidics.PrimeBufferandClearLines());
                }
                fluidicTask.Start();
                await fluidicTask;
            } //done

            //public async void ControlInsTemp()
            //{

            //    ScriptLog(Severity.Info, "Setting Instrument Temp");
            //    string temp = myParentScript.runTemp;
            //    int incTime = myParentScript.incTime;
            //    bool ambientSensing = false;
            //    double ambientTemp = 0;
            //    double startTemp = 63.0;
            //    double lowerTemp = 43.0;
            //    int turnDownTimeSec = 180;


            //    if (temp == "Cool")
            //    {
            //        ScriptLog(Severity.Info, "Cooling...");
            //        tempBlock.ControlTemperature(-10);
            //    }
            //    else if (temp == "RoomTemp")
            //    {
            //        ScriptLog(Severity.Info, "Keeping at Room Temp...");
            //        tempBlock.ControlTemperature(20);
            //    }
            //    else
            //    {
            //        ScriptLog(Severity.Info, "Heating...");
            //        if (ambientSensing)
            //        {
            //            //check ambient temperature
            //            ambientTemp = tempBlock.GetTemperature(2);
            //            startTemp = startTemp + ((4 / 3) * (24 - ambientTemp));
            //            lowerTemp = lowerTemp + ((24 - ambientTemp));
            //        }
            //        //set starting temperature
            //        tempBlock.ControlTemperature(startTemp);
            //    }

            //    tempTask = new Task(() => tempBlock.MonitorandTurnDownTemp(temp, turnDownTimeSec, lowerTemp, 1000));
            //    tempTask.Start();
            //    await tempTask;

            //} //done

            public async void ControlInsTemp()
            {

                ScriptLog(Severity.Control, "Setting Instrument Temp");
                string temp = myParentScript.runTemp;
                int incTime = myParentScript.incTime;
                bool ambientSensing = true;
                double ambientTemp = 0;
                double startTemp = 63.0;
                double lowerTemp = 43.0;
                int turnDownTimeSec = 180;


                if (temp == "Cool")
                {
                    ScriptLog(Severity.Info, "Cooling...");
                    ScriptLog(Severity.Control, "Cooling...");
                    tempBlock.ControlTemperature(-10);
                }
                else if (temp == "RoomTemp")
                {
                    ScriptLog(Severity.Info, "Keeping at Room Temp...");
                    ScriptLog(Severity.Control, "Keeping at Room Temp...");
                    tempBlock.ControlTemperature(20);
                }
                else
                {
                    ScriptLog(Severity.Info, "Heating...");
                    ScriptLog(Severity.Control, "Heating...");
                    if (ambientSensing)
                    {
                        //check ambient temperature
                        ambientTemp = -900;
                        while (ambientTemp < -100)
                        {
                            ambientTemp = tempBlock.GetTemperature(2);
                        }
                        ScriptLog(Severity.Control, "Ambient Temperature = " + ambientTemp);
                        ScriptLog(Severity.Control, "Heat Sink Temperature = " + tempBlock.GetTemperature(3));

                        #region Currently Disabled Ambient Sensing For Ramp
                        //startTemp = 65 - ((ambientTemp - 31) * 2);
                        //if (startTemp > 63)
                        //{
                        //    startTemp = 63;
                        //}
                        #endregion

                        startTemp = 57;
                        ScriptLog(Severity.Control, "Starting Temp Set To: " + startTemp);
                    }
                    //set starting temperature
                    tempBlock.ControlTemperature(startTemp);
                }

                tempTask = new Task(() => tempBlock.MonitorandTurnDownTemp(temp, turnDownTimeSec, lowerTemp, 1000));
                tempTask.Start();
                await tempTask;

            } //done

            public async void ControlInsTemp_ProtoExplorer(string type, int incubTime, int desiredTemp)
            {

                ScriptLog(Severity.Info, "Setting Instrument Temp");
                string tempStyle = type; //myParentScript.runTemp;
                int incTime = incubTime; // myParentScript.incTime;
                bool ambientSensing = true;
                double ambientTemp = 0;
                double startTemp = 63.0;
                double lowerTemp = 43.0;
                int turnDownTimeSec = 180;


                if (tempStyle == "steady")
                {
                    //ScriptLog(Severity.Info, "Cooling...");
                    ScriptLog(Severity.Info, "Steady Style Selected");
                    ScriptLog(Severity.Info, $"| Holding Temp at {desiredTemp}C");
                    tempBlock.ControlTemperature(desiredTemp);
                }
                else if (tempStyle == "ffpe")
                {
                    //ScriptLog(Severity.Info, "Keeping at Room Temp...");
                    ScriptLog(Severity.Info, "FFPE Style Selected");
                    ScriptLog(Severity.Info, $"| Holding Temp at {desiredTemp}C with no fan");
                    tempBlock.ffpeControlTemperature(desiredTemp);
                }
                else
                {
                    //ScriptLog(Severity.Info, "Heating...");
                    ScriptLog(Severity.Info, "Ramp Down Style Selected");
                    if (ambientSensing)
                    {
                        //check ambient temperature
                        ambientTemp = -900;
                        while (ambientTemp < -100)
                        {
                            ambientTemp = tempBlock.GetTemperature(2);
                        }
                        ScriptLog(Severity.Info, "|-- Ambient Temperature = " + ambientTemp + "|| Heat Sink Temperature = " + tempBlock.GetTemperature(3));
                        //ScriptLog(Severity.Control, "Heat Sink Temperature = " + tempBlock.GetTemperature(3));

                        #region Currently Disabled Ambient Sensing For Ramp
                        //startTemp = 65 - ((ambientTemp - 31) * 2);
                        //if (startTemp > 63)
                        //{
                        //    startTemp = 63;
                        //}
                        #endregion

                        startTemp = desiredTemp; //57;
                        ScriptLog(Severity.Info, "|- Starting Temp Set To: " + startTemp);
                    }
                    //set starting temperature
                    tempBlock.ControlTemperature(startTemp);
                }

                tempTask = new Task(() => tempBlock.CellTempToggle_ProtoExplorer(tempStyle, turnDownTimeSec, lowerTemp, 1000));
                tempTask.Start();
                await tempTask;

            }

            public async void monitoringTempBlock()
            {
                tempTask = new Task(() => tempBlock.monitorTemp(1000));
                tempTask.Start();
                await tempTask;
            }



            #endregion

            #region Support Functions

            public double rampTemp = 57;

            public void StopTemp()
            {
                if (initialized)
                {
                    tempBlock.Stop();
                }
            }

            public void SetTempParams()
            {
                ScriptLog(Severity.Control, "Sending TEC Parameters");
                tempBlock.SetTECParameters();
            }

            public void SetTempParams(double setP)
            {
                ScriptLog(Severity.Control, "Sending TEC Parameters");
                tempBlock.SetTECParameters(setP);
            }

            public int Heat()
            {
                //heat
                //tempBlock.SetParamsAndControlTemperature(rampTemp);

                //calculate time for block to reach 63
                tempStartTime = DateTime.UtcNow;
                double startTemp = -999;
                while (startTemp < -900)
                {
                    startTemp = tempBlock.GetTemperature();
                }
                int timeToHeat = 0;
                if (startTemp < rampTemp)
                {
                    double tempExtended = Math.Round(startTemp, 0);
                    //timeToHeat = Convert.ToInt32(Math.Round((((-0.0011) * Math.Pow(tempExtended, 2.0)) - ((0.0039) * (tempExtended)) + 5.0337), 0));
                    int tempInteger = Convert.ToInt32(tempExtended);
                    //timeToHeat = 297 + ((15 - tempInteger) * 5);
                    timeToHeat = Convert.ToInt32((-7.5018 * (startTemp)) + 408.46);
                }
                //timeToHeat = timeToHeat * 60;
                expectedTime = timeToHeat;
                return timeToHeat;
            }

            public int flexTemp(int desiredTemp, string style)
            {
                rampTemp = desiredTemp;
                //heat
                tempBlock.SetParamsAndControlTemperature(rampTemp, style);

                //calculate time for block to reach 63
                tempStartTime = DateTime.UtcNow;
                double startTemp = -999;
                while (startTemp < -900)
                {
                    startTemp = tempBlock.GetTemperature();
                }
                int timeToTemp = 0;
                if (rampTemp > 0)
                {
                    if (startTemp < rampTemp)
                    {
                        double tempExtended = Math.Round(startTemp, 0);
                        //timeToHeat = Convert.ToInt32(Math.Round((((-0.0011) * Math.Pow(tempExtended, 2.0)) - ((0.0039) * (tempExtended)) + 5.0337), 0));
                        int tempInteger = Convert.ToInt32(tempExtended);
                        //timeToHeat = 297 + ((15 - tempInteger) * 5);
                        timeToTemp = Convert.ToInt32((-7.5018 * (startTemp)) + 408.46);
                    }
                    //timeToHeat = timeToHeat * 60;
                }
                else if (rampTemp < 0)
                {
                    if (rampTemp > 2)
                    {
                        double tempRaw = startTemp;
                        double tempExtended = Math.Round(tempRaw, 0);
                        int temp = Convert.ToInt32(tempExtended);

                        timeToTemp = Convert.ToInt32(Math.Round((((-0.233) * Math.Pow(tempRaw, 2.0)) + ((27.889) * (tempRaw)) + 26.78), 2));
                    }
                }
                else
                {
                    ScriptLog(Severity.Control, "Temp was set to 0");
                }

                expectedTime = timeToTemp;
                return timeToTemp;
            }

            public int Cool()
            {
                //cool
                //tempBlock.SetParamsAndControlTemperature(2);
                //tempBlock.ControlTemperature(-1);

                //calculate time for block to reach 0C
                tempStartTime = DateTime.UtcNow;
                double startTemp = -999;
                while (startTemp < -900)
                {
                    startTemp = tempBlock.GetTemperature();
                }
                int timeToCool = 0;
                if (startTemp > 2)
                {
                    //calculate original time to cool
                    double tempRaw = startTemp;
                    double tempExtended = Math.Round(tempRaw, 0);
                    int temp = Convert.ToInt32(tempExtended);

                    timeToCool = Convert.ToInt32(Math.Round((((-0.233) * Math.Pow(tempRaw, 2.0)) + ((27.889) * (tempRaw)) + 26.78), 2));

                    //if (startTemp < 5)
                    //{
                    //    //ticks++;
                    //    timeToCool = Convert.ToInt32(Math.Round((((-0.0348) * Math.Pow(tempRaw, 2.0)) + ((0.8515) * (tempRaw))), 2));
                    //}
                    //else
                    //{
                    //    if (startTemp < 10)
                    //    {
                    //        timeToCool = (Convert.ToInt32(Math.Round((((-0.0251) * Math.Pow(temp, 2.0)) + ((0.8083) * (temp))), 0)));
                    //    }
                    //    else if (startTemp < 15)
                    //    {
                    //        timeToCool = (Convert.ToInt32(Math.Round((((-0.019) * Math.Pow(temp, 2.0)) + ((0.7618) * (temp))), 0)));
                    //    }
                    //    else if (startTemp < 20)
                    //    {
                    //        timeToCool = (Convert.ToInt32(Math.Round((((-0.0153) * Math.Pow(temp, 2.0)) + ((0.7232) * (temp))), 0)));
                    //    }
                    //    else if (startTemp < 30)
                    //    {
                    //        timeToCool = (Convert.ToInt32(Math.Round((((-0.0092) * Math.Pow(temp, 2.0)) + ((0.6223) * (temp)) + 0.2547), 0)));
                    //    }
                    //    else if (startTemp < 40)
                    //    {
                    //        timeToCool = (Convert.ToInt32(Math.Round((((-0.0048) * Math.Pow(temp, 2.0)) + ((0.5014) * (temp)) + 0.5194), 0)));
                    //    }
                    //    else if (startTemp < 50)
                    //    {
                    //        timeToCool = (Convert.ToInt32(Math.Round((((-0.0048) * Math.Pow(temp, 2.0)) + ((0.5014) * (temp)) + 0.5194), 0)));
                    //    }
                    //    else
                    //    {
                    //        timeToCool = (Convert.ToInt32(Math.Round((((-0.0031) * Math.Pow(temp, 2.0)) + ((0.4278) * (temp)) + 0.8941), 0))) + 1;
                    //    }

                    //}
                }
                //timeToCool = timeToCool * 60;
                expectedTime = timeToCool;
                return timeToCool;
            }

            public bool CheckTemp(int machineState)
            {
                bool tempReached = false;
                double myTemp = -999;
                while (myTemp < -100)
                {
                    myTemp = tempBlock.GetTemperature();
                }
                if (machineState == 3)
                {
                    if (myTemp >= rampTemp)
                    {
                        tempReached = true;
                    }
                }
                else
                {
                    if (myTemp <= rampTemp)
                    {
                        tempReached = true;
                    }
                }
                return tempReached;
            }

            public void ReportTemp()
            {
                Thread.Sleep(1000);
                double blockTemp = -999;
                //while (blockTemp < -100)
                //{
                blockTemp = tempBlock.GetTemperature();
                //ScriptLog(Severity.Control, "Block Temperature = " + blockTemp.ToString());
                //}
                double ambTemp = -999;
                //while (ambTemp < -100)
                //{
                ambTemp = tempBlock.GetTemperature(2);
                //ScriptLog(Severity.Control, "Ambient Temperature = " + ambTemp.ToString());
                //}

                //ScriptLog(Severity.Control, "Ambient Temperature = " + ambTemp.ToString());
                //ScriptLog(Severity.Control, "Block Temperature = " + blockTemp.ToString());
                //ScriptLog(Severity.Control, "Heat Sink Temperature = " + tempBlock.GetTemperature(3));

                ScriptLog(Severity.Control, $"|- Block Temp: {blockTemp.ToString()}C, Ambient Temp: {ambTemp.ToString()}C, Heat Sink Temp: {tempBlock.GetTemperature(3)}C");
            }

            public int AdjustTempTime(int machineState)
            {
                int tempTimeAdjustment = 0;
                double secondsElapsed = (DateTime.UtcNow - tempStartTime).TotalSeconds;
                double secondsLeft = expectedTime - secondsElapsed;
                double newSecondsLeft;
                if (machineState == 2)
                {
                    //cooling
                    double tempRaw = -999;
                    while (tempRaw < -100)
                    {
                        tempRaw = tempBlock.GetTemperature();
                    }
                    double tempExtended = Math.Round(tempRaw, 0);
                    int temp = Convert.ToInt32(tempExtended);
                    if (tempRaw < 5)
                    {
                        newSecondsLeft = 60 * (((-0.0348) * Math.Pow(tempRaw, 2.0)) + ((0.8515) * (tempRaw)));
                    }
                    else
                    {
                        if (tempRaw < 10)
                        {
                            newSecondsLeft = ((-0.0251) * Math.Pow(temp, 2.0)) + ((0.8083) * (temp));
                        }
                        else if (tempRaw < 15)
                        {
                            newSecondsLeft = ((-0.019) * Math.Pow(temp, 2.0)) + ((0.7618) * (temp));
                        }
                        else if (tempRaw < 20)
                        {
                            newSecondsLeft = ((-0.0153) * Math.Pow(temp, 2.0)) + ((0.7232) * (temp));
                        }
                        else if (tempRaw < 30)
                        {
                            newSecondsLeft = ((-0.0092) * Math.Pow(temp, 2.0)) + ((0.6223) * (temp)) + 0.2547;
                        }
                        else if (tempRaw < 40)
                        {
                            newSecondsLeft = ((-0.0048) * Math.Pow(temp, 2.0)) + ((0.5014) * (temp)) + 0.5194;
                        }
                        else if (tempRaw < 50)
                        {
                            newSecondsLeft = ((-0.0048) * Math.Pow(temp, 2.0)) + ((0.5014) * (temp)) + 0.5194;
                        }
                        else
                        {
                            newSecondsLeft = (((-0.0031) * Math.Pow(temp, 2.0)) + ((0.4278) * (temp)) + 0.8941) + 1;
                        }

                    }
                }
                else
                {
                    //heating
                    double tempRaw = -999;
                    while (tempRaw < -100)
                    {
                        tempRaw = tempBlock.GetTemperature();
                    }
                    double tempExtended = Math.Round(tempRaw, 0);
                    newSecondsLeft = (((-0.0011) * Math.Pow(tempExtended, 2.0)) - ((0.0039) * (tempExtended)) + 5.0337);
                }
                newSecondsLeft = newSecondsLeft * 60;
                tempTimeAdjustment = Convert.ToInt32(Math.Round((newSecondsLeft - secondsLeft), 0));
                return tempTimeAdjustment;
            }

            public void FactorOffset(int[] zProfile)
            {
                for (int i = 0; i < zProfile.Count(); i++)
                {
                    zProfile[i] = zProfile[i] + grinderVertOffset;
                }
            }

            public void ExtendRunTime(int stepTime)
            {
                if (!myParentScript.abortRequested)
                {
                    duration = (endTime - startTime).TotalSeconds;
                    diff = duration - stepTime;

                    timeRemaining = Convert.ToInt32(timeRemaining - duration);
                    int timeRemainingMins = timeRemaining / 60;
                    int timeRemainingSecs = timeRemaining % 60;

                    if ((diffMs + (diff % 1)) >= Convert.ToInt16(diffMs) + 1)
                    {
                        ScriptLog(Severity.Control, "|- Extend: " + (Convert.ToInt32(diff) + 1)); //extend the expected runtime
                        ScriptLog(Severity.Control, "|- Time Remaining: " + timeRemaining);
                    }
                    else
                    {
                        ScriptLog(Severity.Control, "|- Extend: " + Convert.ToInt32(diff)); //extend the expected runtime
                        ScriptLog(Severity.Control, "|- Time Remaining: " + timeRemaining);
                    }
                    diffMs = diffMs + (diff % 1);
                }
            }

            public async void TempBackToZero()
            {
                ScriptLog(Severity.Info, "Resetting Temperature...");
                ScriptLog(Severity.Control, "Rebounding block temperature..." + DateTime.UtcNow);
                //set block temp back to 0C
                tempBlock.ControlTemperature(0);
                tempTask = new Task(() => tempBlock.TurnTempBackToZeroC());
                tempTask.Start();
                await tempTask;
            }

            public void ReportCOMError()
            {
                //FIXTHIS
                myParentScript.cusMsgBox2.Show("Error Connecting to Instrument.\n Cannot run function.\n ", "Error Connecting to Instrument!", MessageBoxButtons.OK);
                //MessageBox.Show("Error Connecting to Instrument.\n Cannot run function.\n ", "Error Connecting to Instrument!", MessageBoxButtons.OK);
            }

            #endregion

            #region Abort Functions

            public int CalcAbortTime()
            {
                int totAbortTime = 90;

                return totAbortTime;
            }

            public void Abort()
            {
                //localAbort = false; //turns all functions back on
                if (!aborted)
                {
                    string runType = myParentScript.runOutput;
                    //#region Make Temp Safe Part I
                    //ScriptLog(Severity.Info, "   Making Temperature Safe...");
                    ////Option 1: Control temperature to 20C then turn off temp control
                    //tempBlock.ControlTemperature(20);
                    //#endregion

                    myParentScript.aborting = true;

                    #region Make Fluidics Safe
                    ScriptLog(Severity.Info, "   Making Fluidics Safe...");
                    ScriptLog(Severity.Control, "     Delivery Started?" + deliveryStarted);
                    ScriptLog(Severity.Control, "     Delivery Finished?" + deliveryFinished);
                    ScriptLog(Severity.Control, "     Enzyme Loaded?" + enzymeLoaded);
                    fluidics.Initialize(2);
                    if (deliveryStarted && !deliveryFinished && !myParentScript.cannulaProb)
                    {
                        //make sure cartridge is inserted
                        ScriptLog(Severity.Info, "     Flushing Cannulas...");
                        fluidics.FlushCannulas();
                    }
                    if ((runType == "Cells" || myParentScript.rSource == "SingleShot") && enzymeLoaded)
                    {
                        ScriptLog(Severity.Info, "     Cleaning Single Shot...");
                        fluidics.CleanupCellRun();
                    }
                    else
                    {
                        ScriptLog(Severity.Info, "     Cleaning Internals...");
                        fluidics.Cleanup();
                    }
                    #endregion

                    #region Make Rotation Safe
                    ScriptLog(Severity.Info, "   Making Rotator Safe...");
                    rotator.MotorStop();
                    rotator.Cleanup();
                    #endregion

                    #region Make Temp Safe Part II
                    ////ScriptLog(Severity.Info, "   Checking Temperature...");
                    ////ScriptLog(Severity.Info, "     Temp = " + tempBlock.GetTemperature());
                    ////while (tempBlock.GetTemperature() > 55.0 || tempBlock.GetTemperature() < 4.0)
                    ////{
                    ////    Thread.Sleep(1000);
                    ////    ScriptLog(Severity.Info, "     Temp = " + tempBlock.GetTemperature());
                    ////}
                    //WaitTempAsync();
                    tempBlock.Stop();
                    tempBlock.Cleanup();
                    #endregion

                    #region Make Vertical Safe
                    ScriptLog(Severity.Info, "   Making Z-Axis Safe...");
                    verticalStage.Initialize();
                    verticalStage.Cleanup();
                    #endregion

                    if (myParentScript.cannulaProb)
                    {
                        //ask user to insert cleaning cartridge
                        DialogResult cannulaResponse = DialogResult.Cancel;
                        while (cannulaResponse != DialogResult.OK)
                        {
                            //FIXTHIS
                            cannulaResponse = myParentScript.cusMsgBox2.Show("Insert decontamination cartridge then press OK.", "Cleaning Fluidics", MessageBoxButtons.OKCancel);
                            //cannulaResponse = MessageBox.Show("Insert decontamination cartridge then press OK.", "Cleaning Fluidics", MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                        }
                        //rinse cannulas
                        fluidics.RinseCannulas();
                        //dry cannulas
                        fluidics.BlowCannulas();
                    }

                    aborted = true;
                }
            }

            public void explorerAbort()
            {

                ScriptLog(Severity.Control, "|- Cleaning Up Devices");
                ScriptLog(Severity.Control, "|-- Disconnected Fluidics");
                fluidics.Discon();
                ScriptLog(Severity.Control, "|-- Disconnected DC");
                rotator.Cleanup();
                ScriptLog(Severity.Control, "|-- Disconnected LAIRD");
                tempBlock.Cleanup();
                ScriptLog(Severity.Control, "|-- Disconnected Stepper");
                verticalStage.Cleanup();
                ScriptLog(Severity.Control, "|- Safely Exited Explorer");
            }

            #endregion

            #region Maintenance Functions

            public bool InstructUser(string task, string lines)
            {
                DialogResult decision;
                if (task == "Diagnostics")
                {
                    //FIXTHIS
                    decision = myParentScript.cusMsgBox2.Show("Disconnect reagent bottles from instrument then press OK.", "Instrument Diagnostic", MessageBoxButtons.OKCancel);
                    //decision = MessageBox.Show("Disconnect reagent bottles from instrument then press OK.", "Instrument Diagnostic", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2);
                }
                else if (task == "Calibration")
                {
                    //FIXTHIS
                    decision = myParentScript.cusMsgBox2.Show("Insert Calibration Cartridge then press OK.", "Instrument Calibration", MessageBoxButtons.OKCancel);
                    //decision = MessageBox.Show("Insert Calibration Cartridge then press OK.", "Instrument Calibration", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2);
                }
                else if (task == "Clean")
                {
                    if (lines == "Cells")
                    {
                        //FIXTHIS
                        decision = myParentScript.cusMsgBox2.Show("Load 15mL tubes filled with 5mL 10% bleach onto Single-Shot Assembly." + Environment.NewLine +
                            "Check water and waste bottles for sufficient volumes." + Environment.NewLine + " ", "Sanitize Single-Shot", MessageBoxButtons.OKCancel);
                        //decision = MessageBox.Show("Load 15mL tubes filled with 5mL 10% bleach onto Single-Shot Assembly." + Environment.NewLine +
                        //    "Check water and waste bottles for sufficient volumes." + Environment.NewLine + " ", "Sanitize Single-Shot", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2);
                    }
                    else if (lines == "Nuclei")
                    {
                        //FIXTHIS
                        decision = myParentScript.cusMsgBox2.Show("Load 15mL tubes filled with 5mL 10% bleach onto Single-Shot Assembly." + Environment.NewLine +
                            "Make sure nuclei lines are placed in empty bottles." + Environment.NewLine +
                            "Check water and waste bottles for sufficient volumes." + Environment.NewLine + " ", "Sanitize Nuclei Lines", MessageBoxButtons.OKCancel);
                        //decision = MessageBox.Show("Load 15mL tubes filled with 5mL 10% bleach onto Single-Shot Assembly." + Environment.NewLine +
                        //    "Make sure nuclei lines are placed in empty bottles." + Environment.NewLine +
                        //    "Check water and waste bottles for sufficient volumes." + Environment.NewLine + " ", "Sanitize Nuclei Lines", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2);
                    }
                    else //lines = All
                    {
                        //FIXTHIS
                        decision = myParentScript.cusMsgBox2.Show("Load 15mL tubes filled with 10mL 10% bleach onto Single-Shot Assembly" + Environment.NewLine +
                            "Make sure nuclei lines are placed in empty bottles." + Environment.NewLine +
                            "Check water and waste bottles for sufficient volumes." + Environment.NewLine +
                            "Insert empty Decontamination Cartridge into instrument." + Environment.NewLine + " ", "Sanitize All Lines", MessageBoxButtons.OKCancel);
                        //decision = MessageBox.Show("Load 15mL tubes filled with 10mL 10% bleach onto Single-Shot Assembly" + Environment.NewLine +
                        //    "Make sure nuclei lines are placed in empty bottles." + Environment.NewLine +
                        //    "Check water and waste bottles for sufficient volumes." + Environment.NewLine +
                        //    "Insert empty Decontamination Cartridge into instrument." + Environment.NewLine + " ", "Sanitize All Lines", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2);
                    }
                }
                else //task = Rinse
                {
                    if (lines == "Cells")
                    {
                        //FIXTHIS
                        decision = myParentScript.cusMsgBox2.Show("Load empty 15mL tubes onto Single-Shot Assembly." + Environment.NewLine +
                            "Check water and waste bottles for sufficient volumes." + Environment.NewLine + " ", "Sanitize Single-Shot", MessageBoxButtons.OKCancel);
                        //decision = MessageBox.Show("Load empty 15mL tubes onto Single-Shot Assembly." + Environment.NewLine +
                        //    "Check water and waste bottles for sufficient volumes." + Environment.NewLine + " ", "Sanitize Single-Shot", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2);
                    }
                    else if (lines == "Nuclei")
                    {
                        //FIXTHIS
                        decision = myParentScript.cusMsgBox2.Show("Make sure nuclei lines are placed in empty bottles." + Environment.NewLine +
                            "Check water and waste bottles for sufficient volumes." + Environment.NewLine + " ", "Sanitize Nuclei Lines", MessageBoxButtons.OKCancel);
                        //decision = MessageBox.Show("Make sure nuclei lines are placed in empty bottles." + Environment.NewLine +
                        //    "Check water and waste bottles for sufficient volumes." + Environment.NewLine + " ", "Sanitize Nuclei Lines", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2);
                    }
                    else //lines = All
                    {
                        //FIXTHIS
                        decision = myParentScript.cusMsgBox2.Show("Load empty 15mL tubes onto Single-Shot Assembly." + Environment.NewLine +
                            "Make sure nuclei lines are placed in empty bottles." + Environment.NewLine +
                            "Check water and waste bottles for sufficient volumes." + Environment.NewLine +
                            "Insert empty Decontamination Cartridge into instrument." + Environment.NewLine + " ", "Sanitize All Lines", MessageBoxButtons.OKCancel);
                        //decision = MessageBox.Show("Load empty 15mL tubes onto Single-Shot Assembly." + Environment.NewLine +
                        //    "Make sure nuclei lines are placed in empty bottles." + Environment.NewLine +
                        //    "Check water and waste bottles for sufficient volumes." + Environment.NewLine +
                        //    "Insert empty Decontamination Cartridge into instrument." + Environment.NewLine + " ", "Sanitize All Lines", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2);
                    }
                }
                if (decision == DialogResult.Cancel)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            public int CalcMaintenanceTime()
            {
                int totalMaintTime = 0;
                clearlinestime = 30;
                clearlinestime2 = 30;
                cleanlinestime = 30;
                rinselinestime = 30;
                if (lineInput == "All")
                {
                    clearlinestime = 75; //2.5x plain clearlinestime
                    clearlinestime2 = 75; //2.5x
                    cleanlinestime = 75; //2.5x
                    rinselinestime = 75; //2.5x
                }

                if (maintenanceTask == "Clean")
                {
                    totalMaintTime = clearlinestime + cleanlinestime + (4 * (clearlinestime2 + rinselinestime));
                }
                else if (maintenanceTask == "Rinse")
                {
                    totalMaintTime = (2 * clearlinestime) + rinselinestime;
                }
                else if (maintenanceTask == "Calibration")
                {
                    totalMaintTime = calibrateTime;
                }
                else
                {
                    totalMaintTime = stepperdiagtime + dcdiagtime + fluidicdiagtime + tempdiagtime;
                }

                return totalMaintTime;
            }

            public void ClearLines(string type) //ready for testing 7-31-20
            {
                ScriptLog(Severity.Info, "Blow Out Lines");
                startTime = DateTime.UtcNow;
                //Prime Air
                ScriptLog(Severity.Info, "  Prime Air");
                fluidics.ClearLines();
                //Clear Cartridge
                if (type == "All")
                {
                    ScriptLog(Severity.Info, "  Blow Out Cannulas");
                    fluidics.BlowCannulas();
                }
                //Clear NIR/NSR
                if (type == "Nuclei" || type == "All")
                {
                    ScriptLog(Severity.Info, "  Blow Out Nuclei Lines");
                    fluidics.BlowNIRNSR();
                }
                if (type == "Cells" || type == "All")
                {
                    if (myParentScript.myInstrument.maintenanceTask == "Rinse")
                    {
                        ScriptLog(Severity.Info, "  Blow Out Cell Lines");
                        fluidics.BlowEnzBuffer();
                    }
                }

                endTime = DateTime.UtcNow;
                //ExtendRunTime(clearlinestime);
            }

            public void ClearLines2(string type) //ready for testing 7-31-20
            {
                ScriptLog(Severity.Info, "Clear Lines");
                startTime = DateTime.UtcNow;
                //Prime Air
                ScriptLog(Severity.Info, "  Prime Air");
                fluidics.ClearLines();
                //Clear Cartridge
                if (type == "All")
                {
                    ScriptLog(Severity.Info, "  Blow Out Cannulas");
                    fluidics.BlowCannulas();
                }
                //Clear Enz/Buffer Lines
                if (type == "Cells" || type == "All")
                {
                    ScriptLog(Severity.Info, "  Blow Out Cell Lines");
                    fluidics.BlowEnzBuffer();
                }
                //Clear NIR/NSR
                if (type == "Nuclei" || type == "All")
                {
                    ScriptLog(Severity.Info, "  Blow Out Nuclei Lines");
                    fluidics.BlowNIRNSR();
                    if (myParentScript.myInstrument.maintenanceTask == "Clean")
                    {
                        fluidics.BlowEnzBuffer();
                    }
                }
                endTime = DateTime.UtcNow;
                //ExtendRunTime(clearlinestime2);
            }

            public void RinseLines(string type) //ready for testing 7-31-20
            {
                ScriptLog(Severity.Info, "Rinse Lines");
                startTime = DateTime.UtcNow;
                //Prime Water
                ScriptLog(Severity.Info, "  Prime Water");
                fluidics.PrimeWater();
                //Rinse Cartridge
                if (type == "All")
                {
                    ScriptLog(Severity.Info, "  Rinse Cannulas");
                    fluidics.RinseCannulas();
                    ScriptLog(Severity.Info, "  Rinse Cell Lines");
                    fluidics.RinseEnzBuffer();
                    ScriptLog(Severity.Info, "  Rinse Nuclei Lines");
                    fluidics.RinseNIRNSR();
                }
                //Rinse Enz/Buffer
                if (type == "Cells")
                {
                    ScriptLog(Severity.Info, "  Rinse Cell Lines");
                    fluidics.RinseEnzBuffer();
                }
                //Rinse NIR/NSR
                if (type == "Nuclei")
                {
                    ScriptLog(Severity.Info, "  Rinse Nuclei Lines");
                    fluidics.RinseNIRNSR();
                    if (myParentScript.myInstrument.maintenanceTask == "Clean")
                    {
                        ScriptLog(Severity.Info, "  Rinse Cell Lines");
                        fluidics.RinseEnzBuffer();
                    }
                }
                endTime = DateTime.UtcNow;
                //ExtendRunTime(rinselinestime);
            }

            public void CleanLines(string type) //ready for testing 7-31-20
            {
                ScriptLog(Severity.Info, "Clean Lines");
                startTime = DateTime.UtcNow;
                //Decon Solution is now going to be on single shot lines
                if (type == "Cells")
                {
                    ScriptLog(Severity.Info, "  Clean Cell Lines");
                    fluidics.CleanEnzBuffer();
                }
                else
                {
                    //Prime Decon Solution
                    ScriptLog(Severity.Info, "  Prime Cleaning Solution");
                    fluidics.PrimeDecon();
                    //Clean Cartridge (need to change to single shot lines
                    if (type == "All")
                    {
                        ScriptLog(Severity.Info, "  Clean Cannulas");
                        fluidics.CleanCannulas();
                        ScriptLog(Severity.Info, "  Clean Nuclei Lines");
                        fluidics.CleanNIRNSR();
                        ScriptLog(Severity.Info, "  Clean Cell Lines");
                        fluidics.CleanEnzBuffer();
                    }
                    //Clean NIR/NSR
                    else if (type == "Nuclei")
                    {
                        ScriptLog(Severity.Info, "  Clean Nuclei Lines");
                        fluidics.CleanNIRNSR();
                        fluidics.CleanNIRNSR();
                    }
                }
                endTime = DateTime.UtcNow;
                //ExtendRunTime(cleanlinestime);
            }

            public void StepperDiagnostics() //not ready 7-31-20
            {
                ////Initialize the stepper then move -10000 steps then find x steps to home sensor
                ////move 100000 steps down and then back to -10000 point and find y steps to home sensor
                ////Comparing x and y steps indicates stepper drift
                ScriptLog(Severity.Info, "Beginning Z-Axis Diagnostic...");
                ScriptLog(Severity.Info, "  Using Controller Board 3.0");
                zAxisPassed = true;
                int negsteps = -500; //was -400


                #region Home Travel Test
                ScriptLog(Severity.Info, "Test Motor Homing...");

                #region Init Stepper
                //verticalStage.SetCurrentAxis(1);
                //verticalStage.SetMotorCurrentScaling(75);
                //verticalStage.SetMaxHomeSearchMove(170000);
                //verticalStage.SetHomingSpeed(20000);
                verticalStage.Initialize();
                //verticalStage.AwaitMoveDone();
                //verticalStage.MoveToWaitDone(20000);
                //verticalStage.Initialize();
                //verticalStage.AwaitMoveDone();
                #endregion

                for (int stepperLoopNum = 0; stepperLoopNum < 11; stepperLoopNum++)
                {
                    Thread.Sleep(1000);
                    verticalStage.SetMaxSpeed(20000); //was 2800 then 5600
                    verticalStage.MoveToWaitDone(negsteps); //was -10000 and steps were in 25 increments

                    Thread.Sleep(1000);

                    #region Creep Loop
                    int creepOne = 0;
                    while (verticalStage.GetFlagState() == "0")
                    {
                        verticalStage.MoveToWaitDone(negsteps + (10 * creepOne));
                        ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState() + ", Position is: " + verticalStage.GetPosition());
                        creepOne++;
                    }
                    string stepLossOne = verticalStage.GetPosition();
                    ScriptLog(Severity.Info, "   Steps to uncover home sensor:" + stepLossOne); //record to log
                    creepResults.Add(stepLossOne);
                    verticalStage.SetMaxSpeed(20000);
                    #endregion

                    //#region Moving Down and Up
                    //verticalStage.MoveToWaitDonewSpeed(90000); //was 25000

                    verticalStage.Initialize();
                    verticalStage.MoveCheckInterlock(120000); //120000 for old driver
                    rotator.RotateOnce(4000, engageRPM, false);
                    verticalStage.MoveToWaitDone(204000); //~101550 for new driver //~205000-1150 = 204000 for old driver

                    //verticalStage.MoveToWaitDone(5000);
                    //verticalStage.MoveToWaitDone(negsteps);
                    //#endregion

                    //#region Second Creep Loop
                    //int creepTwo = 0;
                    //while (verticalStage.GetFlagState() == "0")
                    //{
                    //    verticalStage.MoveToWaitDone(negsteps + (10 * creepTwo));
                    //    ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState() + ", Position is: " + verticalStage.GetPosition());
                    //    creepTwo++;
                    //}
                    //string stepLossTwo = verticalStage.GetPosition();
                    //ScriptLog(Severity.Info, "   New steps to uncover home sensor:" + stepLossTwo); //record to log
                    //#endregion

                    //#region Calculate Difference Between Creeps
                    //int creepDiff = Convert.ToInt32(stepLossTwo) - Convert.ToInt32(stepLossOne);
                    //ScriptLog(Severity.Info, "    Step difference between two tries is " + creepDiff + " steps."); //record to log
                    //if (creepDiff > 50 || creepDiff < -50)
                    //{
                    //    zAxisPassed = false;
                    //}
                    //#endregion

                    #region Re-init Stepper
                    //verticalStage.SetCurrentAxis(1);
                    //verticalStage.SetMotorCurrentScaling(75);
                    //verticalStage.SetMaxHomeSearchMove(170000);
                    //verticalStage.SetHomingSpeed(20000);
                    //verticalStage.Initialize();
                    //verticalStage.AwaitMoveDone();
                    //verticalStage.MoveToWaitDone(20000);
                    //verticalStage.Initialize();
                    //verticalStage.AwaitMoveDone();
                    #endregion

                }

                #region Init Stepper
                //verticalStage.SetCurrentAxis(1);
                //verticalStage.SetMotorCurrentScaling(75);
                //verticalStage.SetMaxHomeSearchMove(170000);
                //verticalStage.SetHomingSpeed(20000);
                verticalStage.Initialize();
                //verticalStage.AwaitMoveDone();
                //verticalStage.MoveToWaitDone(20000);
                //verticalStage.Initialize();
                //verticalStage.AwaitMoveDone();
                #endregion

                #endregion

                #region EOT Travel Test (Need to do 7/31/20)
                //for (int stepperLoopNum2 = 0; stepperLoopNum2 < 20; stepperLoopNum2++)
                //{

                //    verticalStage.MoveToWaitDone(-5000);

                //    #region First Creep Loop
                //    int creepOne = 1;
                //    while (verticalStage.GetFlagState() == "0")
                //    {
                //        //ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState());
                //        verticalStage.MoveToWaitDone(-5000 + (25 * creepOne));
                //        creepOne++;
                //    }
                //    string stepLossOne = verticalStage.GetPosition();
                //    ScriptLog(Severity.Info, "   Steps to uncover home sensor:" + stepLossOne); //record to log
                //    #endregion

                //    verticalStage.MoveToWaitDone(50000);
                //    verticalStage.MoveToWaitDone(-5000);

                //    #region Second Creep Loop
                //    int creepTwo = 1;
                //    while (verticalStage.GetFlagState() == "0")
                //    {
                //        //ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState());
                //        verticalStage.MoveToWaitDone(-10000 + (100 * creepTwo));
                //        creepTwo++;
                //    }
                //    string stepLossTwo = verticalStage.GetPosition();
                //    ScriptLog(Severity.Info, "   New steps to uncover home sensor:" + stepLossTwo); //record to log
                //    #endregion

                //    #region Calculate Difference Between Creeps
                //    int creepDiff = Convert.ToInt32(stepLossTwo) - Convert.ToInt32(stepLossOne);
                //    ScriptLog(Severity.Info, "    Step difference between two tries is " + creepDiff + " steps."); //record to log
                //    if (creepDiff > 100)
                //    {
                //        zAxisPassed = false;
                //    }
                //    #endregion

                //    #region Re-init Stepper
                //    verticalStage.SetCurrentAxis(1);
                //    verticalStage.SetMotorCurrentScaling(75);
                //    verticalStage.SetMaxHomeSearchMove(170000);
                //    verticalStage.SetHomingSpeed(20000);
                //    verticalStage.Initialize();
                //    verticalStage.AwaitMoveDone();
                //    verticalStage.MoveToWaitDone(20000);
                //    verticalStage.Initialize();
                //    verticalStage.AwaitMoveDone();
                //    #endregion

                //}
                #endregion

                //MessageBox.Show("Test Next Module?", "Stepper Diagnostic Complete", MessageBoxButtons.OK);
            }

            public void DCDiagnostics() //ready for testing 7-31-20
            {
                ////Run DC Motor at Slowest to Fastest Speeds and make sure encoder value reads correctly at each speed/time combo
                ////Run this multiple times?
                
                ScriptLog(Severity.Info, "Beginning Rotational Diagnostic...");
                ScriptLog(Severity.Info, "Firmware Version: " + rotator.GetFirmwareVersion());

                dcPassed = true;

                string direction;
                short RPM = 145;
                for (int dcCycle = 0; dcCycle < 5; dcCycle++)
                {
                    ScriptLog(Severity.Info, "Rotation test at " + RPM + " RPM");
                    for (int dcTestCycle = 0; dcTestCycle < 10; dcTestCycle++)
                    {
                        direction = "forward"; //grinding direction set to forward
                        int encoderPositionOne = rotator.ReadEncoderPosition();
                        if (encoderPositionOne == 0)
                        {
                            dcPassed = false;
                        }
                        //rotator.SetMotorRPM(RPM, direction); //grind at set speed and direction until stop command sent
                        //Thread.Sleep(2000); //Sleep for grind //KC: was 4000
                        //rotator.MotorStop(); //Stop grinding motor
                        rotator.RotateOnceConstVel(2000, RPM, false);
                        int encoderPositionTwo = rotator.ReadEncoderPosition();
                        if (encoderPositionTwo == 0)
                        {
                            dcPassed = false;
                        }
                        int encoderTravelForward = encoderPositionTwo - encoderPositionOne;
                        ScriptLog(Severity.Info, "   Rotated forward " + encoderTravelForward + " steps.");

                        direction = "reverse"; //grinding direction set to reverse
                        //rotator.SetMotorRPM(RPM, direction); //grind at set speed and direction until stop command sent
                        //Thread.Sleep(2000); //Sleep for grind //KC: was 4000
                        //rotator.MotorStop(); //Stop grinding motor
                        rotator.RotateOnceConstVel(2000, (short)(RPM * (-1)), false);
                        encoderPositionOne = rotator.ReadEncoderPosition();
                        if (encoderPositionOne == 0)
                        {
                            dcPassed = false;
                        }
                        int encoderTravelReverse = encoderPositionTwo - encoderPositionOne;
                        ScriptLog(Severity.Info, "   Rotated backward " + encoderTravelReverse + " steps."); //record to log
                        int encoderTravelDiff = encoderTravelForward - encoderTravelReverse;
                        double encoderAvg = (encoderTravelForward + encoderTravelReverse) / 2;
                        ScriptLog(Severity.Info, "     Difference between forward and reverse travel is " + encoderTravelDiff + " steps."); //record to log
                        if (Math.Abs(encoderTravelDiff/encoderAvg) > 0.05)
                        {
                            dcPassed = false;
                        }
                    }
                    RPM = (short)(RPM - 25);
                }
                //MessageBox.Show("Test Next Module?", "DC Diagnostic Complete", MessageBoxButtons.OK);
            }

            public void FluidicDiagnostics() //ready for testing 7-31-20
            {
                ////Can check if lines are clogged with syringe pump by lowering force of syringe pump and pushing on individual lines and seeing if there is an error
                ScriptLog(Severity.Info, "Beginning Fluidic Diagnostic...");

                fluidicsPassed = false;
                bool valveLineResult = true;
                int[] linesToTest = { 3, 4, 5, 6, 7, 8, 10, 11, 12 }; //all lines are 3,4,5,6,7,8,10,11,12
                failedLines = new List<int>();

                fluidics.AdjustPumpPower(20);

                foreach (int line in linesToTest)
                {
                    ScriptLog(Severity.Info, "   Checking fluidic line " + line);
                    valveLineResult = true;
                    valveLineResult = fluidics.CheckLine(line);
                    if (!valveLineResult)
                    {
                        failedLines.Add(line);
                        fluidics.Initialize();
                    }
                }

                fluidics.AdjustPumpPower(50);
                if (failedLines.Count() == 0)
                {
                    fluidicsPassed = true;
                }
                else
                {
                    fluidicsPassed = false;
                }
                //MessageBox.Show("Test Next Module?", "Fluidic Diagnostic Complete", MessageBoxButtons.OK);
            }

            public void TempDiagnostics() //ready for testing 7-31-20
            {
                ScriptLog(Severity.Info, "Beginning Temp Diagnostic...");
                #region Temperature Diagnostics
                ////Check all thermistors are there
                bool failed = false;
                int check = 0;
                double amb = -999;
                while (amb < -900 && check < 3)
                {
                    amb = tempBlock.GetTemperature(2);
                    ScriptLog(Severity.Info, "Amb Temp = " + amb.ToString());
                    check++;
                }
                if (check == 3)
                {
                    failed = true;
                }
                check = 0;
                double hS = -999;
                while (hS < -900 && check < 3)
                {
                    hS = tempBlock.GetTemperature(3);
                    ScriptLog(Severity.Info, "HeatSink Temp = " + hS.ToString());
                    check++;
                }
                if (check == 3)
                {
                    failed = true;
                }
                if (failed)
                {
                    coolTestFailed = true;
                    heatTestFailed = true;
                    ScriptLog(Severity.Info, "Something is wrong with the thermistors, ending Diagnostic.");
                }
                else
                {
                    ////Temperature control to 20degC for 1 min to equilibrate block
                    #region Restore Block to Room Temperature
                    int monitorRTCycles = 0;
                    ScriptLog(Severity.Info, "");
                    ScriptLog(Severity.Info, "   Restoring Block to Room Temp...");
                    tempBlock.ControlTemperature(20);
                    double roomTemp;
                    double ambTemp = 0.0;
                    roomTemp = -999;
                    while (roomTemp < -900)
                    {
                        roomTemp = tempBlock.GetTemperature();
                    }
                    while (roomTemp >= 20.0)
                    {
                        if (monitorRTCycles % 10 == 0)
                        {
                            ambTemp = -999;
                            while (ambTemp < -900)
                            {
                                ambTemp = tempBlock.GetTemperature(2);
                            }
                            ScriptLog(Severity.Info, "     Temp = " + roomTemp + ", Ambient = " + ambTemp);
                        }
                        Thread.Sleep(1000);
                        roomTemp = -999;
                        while (roomTemp < -900)
                        {
                            roomTemp = tempBlock.GetTemperature();
                        }
                        monitorRTCycles++;
                    }
                    Thread.Sleep(60000);
                    #endregion
                    ////Temperature control to 65degC and make sure it gets there under certain time
                    #region Heat Test
                    ScriptLog(Severity.Info, "");
                    ScriptLog(Severity.Info, "   Heating Temperature Block...");
                    tempBlock.ControlTemperature(70);

                    int monitorHeatCycles = 0;
                    var heatingTime = TimeSpan.FromMinutes(1);
                    var heatStartTime = DateTime.UtcNow;
                    bool exitHeatLoop = false;
                    heatTestFailed = false;
                    double heatTemp;
                    heatTemp = -999;
                    while (heatTemp < -900)
                    {
                        heatTemp = tempBlock.GetTemperature();
                    }

                    ScriptLog(Severity.Info, "   Waiting 3 Minutes...");
                    Thread.Sleep(180000);

                    ScriptLog(Severity.Info, "   Monitoring Temperature...");
                    while (heatTemp < 65.0 && exitHeatLoop == false)
                    {
                        heatingTime = (DateTime.UtcNow - heatStartTime);
                        if (heatingTime >= TimeSpan.FromMinutes(30))
                        {
                            exitHeatLoop = true;
                            heatTestFailed = true;
                        }
                        Thread.Sleep(1000);
                        heatTemp = -999;
                        while (heatTemp < -900)
                        {
                            heatTemp = tempBlock.GetTemperature();
                        }
                        if (monitorHeatCycles % 10 == 0)
                        {
                            ambTemp = -999;
                            while (ambTemp < -900)
                            {
                                ambTemp = tempBlock.GetTemperature(2);
                            }
                            ScriptLog(Severity.Info, "     Temp = " + heatTemp + ", Ambient = " + ambTemp);
                        }
                        monitorHeatCycles++;
                    }
                    if (heatTestFailed)
                    {
                        ScriptLog(Severity.Warning, "   Thermal Block did not heat in time.");
                    }
                    else
                    {
                        //ScriptLog(Severity.Info, "     Thermal Block took " + heatingTime + " minutes to heat.");
                        ScriptLog(Severity.Info, "   Thermal Block took " + Math.Round(heatingTime.TotalMinutes, 2) + " minutes to heat.");
                    }

                    if (heatingTime.TotalMinutes > 10.0)
                    {
                        heatTestFailed = true;
                    }
                    #endregion
                    ////Temperature control back to 20degC for 1 min
                    #region Restore Block to Room Temperature
                    monitorRTCycles = 0;
                    ScriptLog(Severity.Info, "");
                    ScriptLog(Severity.Info, "   Restoring Block to Room Temp...");
                    tempBlock.ControlTemperature(20);
                    roomTemp = -999;
                    while (roomTemp < -900)
                    {
                        roomTemp = tempBlock.GetTemperature();
                    }
                    while (roomTemp > 20.0)
                    {
                        if (monitorRTCycles % 10 == 0)
                        {
                            ambTemp = -999;
                            while (ambTemp < -900)
                            {
                                ambTemp = tempBlock.GetTemperature(2);
                            }
                            ScriptLog(Severity.Info, "     Temp = " + roomTemp + ", Ambient = " + ambTemp);
                        }
                        Thread.Sleep(1000);
                        roomTemp = -999;
                        while (roomTemp < -900)
                        {
                            roomTemp = tempBlock.GetTemperature();
                        }
                        monitorRTCycles++;
                    }
                    Thread.Sleep(60000);
                    #endregion
                    ////Temperature control to 0degC and make sure it gets there under certain time
                    #region Cool Test
                    ScriptLog(Severity.Info, "   Cooling Temperature Block...");
                    tempBlock.ControlTemperature(-10);

                    var coolingTime = TimeSpan.FromMinutes(1);
                    var coolStartTime = DateTime.UtcNow;
                    bool exitCoolLoop = false;
                    coolTestFailed = false;
                    double coolTemp;
                    coolTemp = -999;
                    while (coolTemp < -900)
                    {
                        coolTemp = tempBlock.GetTemperature();
                    }
                    int monitorCoolCycles = 0;

                    ScriptLog(Severity.Info, "   Waiting 5 Minutes...");
                    Thread.Sleep(300000);

                    ScriptLog(Severity.Info, "   Monitoring Temperature...");
                    while ((coolTemp > 0.0 || coolTemp < -900.0) && exitCoolLoop == false)
                    {
                        coolingTime = (DateTime.UtcNow - coolStartTime);
                        if (coolingTime >= TimeSpan.FromMinutes(30))
                        {
                            exitCoolLoop = true;
                            coolTestFailed = true;
                        }
                        Thread.Sleep(1000);
                        coolTemp = -999;
                        while (coolTemp < -900)
                        {
                            coolTemp = tempBlock.GetTemperature();
                        }

                        if (monitorCoolCycles % 10 == 0)
                        {
                            ambTemp = -999;
                            while (ambTemp < -900)
                            {
                                ambTemp = tempBlock.GetTemperature(2);
                            }
                            ScriptLog(Severity.Info, "     Temp = " + coolTemp + ", Ambient = " + ambTemp);
                        }
                        monitorCoolCycles++;
                    }
                    if (coolTestFailed)
                    {
                        ScriptLog(Severity.Warning, "     Thermal Block did not cool in time.");
                    }
                    else
                    {
                        //ScriptLog(Severity.Info, "     Thermal Block took " + coolingTime + " minutes to cool.");
                        ScriptLog(Severity.Info, "     Thermal Block took " + Math.Round(coolingTime.TotalMinutes, 2) + " minutes to cool.");
                    }
                    if (coolingTime.TotalMinutes > 12)
                    {
                        coolTestFailed = true;
                    }
                    #endregion
                    tempBlock.Stop();
                }
                #endregion
                //MessageBox.Show("Test Next Module?", "Temperature Diagnostic Complete", MessageBoxButtons.OK);
            }

            public void SummarizeResults() //ready 7-31-20
            {
                //Summarize Results
                string results = "";
                bool allPassed = true;
                ScriptLog(Severity.Info, "Stepper Passed? " + zAxisPassed);
                results += "Stepper Passed = " + zAxisPassed.ToString() + "\n";
                ScriptLog(Severity.Info, "Rotator Passed? " + dcPassed);
                results += "Rotator Passed = " + dcPassed.ToString() + "\n";
                if (!dcPassed || !zAxisPassed)
                {
                    allPassed = false;
                }
                if (coolTestFailed)
                {
                    ScriptLog(Severity.Info, "Cooling Passed? False");
                    //results += "Cooling Passed = False \n";
                    allPassed = false;
                }
                if (heatTestFailed)
                {
                    ScriptLog(Severity.Info, "Heating Passed? False");
                    //results += "Heating Passed = False \n";
                    allPassed = false;
                }
                if (!coolTestFailed && !heatTestFailed)
                {
                    ScriptLog(Severity.Info, "Temp Control Passed? True");
                    results += "Temp Control Passed = True \n";
                }
                else
                {
                    results += "Temp Control Passed = False \n";
                }
                ScriptLog(Severity.Info, "Fluidics Passed? " + fluidicsPassed);
                results += "Fluidics Passed = " + fluidicsPassed.ToString() + "\n" + " ";
                if (!fluidicsPassed)
                {
                    string failedLinesList = string.Join(", ", failedLines.ToArray());
                    ScriptLog(Severity.Info, "Valve Lines " + failedLinesList + " need to be inspected");
                    allPassed = false;
                }
                if (!allPassed)
                {
                    results += "\nPlease contact S2 Genomics Technical Support Team\n" + " ";
                }

                //FIXTHIS
                myParentScript.cusMsgBox2.Show(results, "Diagnostic Complete", MessageBoxButtons.OK);
                //MessageBox.Show(results, "Diagnostic Complete", MessageBoxButtons.OK);
            }

            public void Calibrate()
            {
                int offset = myParentScript.myInstrument.grinderVertOffset;
                string direction;
                short RPM = 95;
                if (offset >= 0)
                {
                    offset = -20000;
                }

                ScriptLog(Severity.Info, "Calibration Started");

                #region 1. Offset Finding Part I

                ScriptLog(Severity.Info, "Determining Bottom 1x Resolution");

                #region A. Initialize Stepper
                ScriptLog(Severity.Info, "Initializing Stepper..."); //record to log
                verticalStage.Initialize();

                //ScriptLog(Severity.Info, "Initializing Syringe and Valves..."); //record to log
                //myCavro.Initialize(3);
                #endregion

                #region B. Creep Loop
                verticalStage.MoveToWaitDone(-5000);
                int f = 1;
                while (verticalStage.GetFlagState() == "0")
                {
                    //ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState());
                    verticalStage.MoveToWaitDone(-5000 + (1000 * f));
                    f++;
                }
                string steploss = verticalStage.GetPosition();
                ScriptLog(Severity.Info, "Steps to uncover home sensor:" + steploss); //record to log
                #endregion

                #region C. Connect to Cap
                ScriptLog(Severity.Info, "Connecting to Grinder Cap..."); //record to log
                verticalStage.MoveCheckInterlock(120000);
                #region Rotate to Connect
                RPM = 95;
                for (int i = 0; i < 1; i++) //grind for 1 cycles
                {
                    direction = "forward"; //grinding direction set to forward
                    rotator.SetMotorRPM(RPM, direction); //grind at set speed and direction until stop command sent
                    Thread.Sleep(4000); //Sleep for grind //KC: was 4000
                    rotator.MotorStop(); //Stop grinding motor

                    //direction = "reverse"; //grinding direction set to reverse
                    //rotator.SetMotorRPM(RPM, direction); //grind at set speed and direction until stop command sent
                    //Thread.Sleep(4000); //Sleep for grind //KC: was 4000
                    //rotator.MotorStop(); //Stop grinding motor
                }
                #endregion
                #endregion

                #region D. Gross Find Bottom

                #region OneStep
                int myStep = 275000; //for the first offset find this needs to be high
                verticalStage.MoveToWaitDone(myStep);
                #endregion

                #endregion

                #region E. Move Stepper Back Up
                ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState());
                ScriptLog(Severity.Info, "Moving Stepper to Position 0");
                verticalStage.MoveToWaitDone(-5000);
                ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState());
                #endregion

                #region F. Creep Loop 2
                f = 1;
                while (verticalStage.GetFlagState() == "0")
                {
                    //ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState());
                    verticalStage.MoveToWaitDone(-5000 + (1000 * f));
                    f++;
                }
                string steploss2 = verticalStage.GetPosition();
                ScriptLog(Severity.Info, "New steps to uncover home sensor:" + steploss2); //record to log
                #endregion

                #region G. Calculate Gross Offset
                int overallLoss = Convert.ToInt32(steploss2) - Convert.ToInt32(steploss);
                int bottom = myStep - overallLoss;
                ScriptLog(Severity.Info, "Bottom is around:" + bottom);
                #endregion

                ScriptLog(Severity.Info, "Offset Measuring Part I Complete.");
                #endregion

                #region 2. Offset Finding Part II

                ScriptLog(Severity.Info, "Determining Bottom 10x Resolution");

                #region A. Initialize Stepper
                ScriptLog(Severity.Info, "Initializing Stepper..."); //record to log
                verticalStage.Initialize();

                //ScriptLog(Severity.Info, "Initializing Syringe and Valves..."); //record to log
                //myCavro.Initialize(3);
                #endregion

                #region B. Creep Loop
                verticalStage.MoveToWaitDone(-500);
                f = 1;
                while (verticalStage.GetFlagState() == "0")
                {
                    //ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState());
                    verticalStage.MoveToWaitDone(-500 + (100 * f));
                    f++;
                }
                steploss = verticalStage.GetPosition();
                ScriptLog(Severity.Info, "Steps to uncover home sensor:" + steploss); //record to log
                #endregion

                #region C. Connect to Cap
                ScriptLog(Severity.Info, "Connecting to Grinder Cap..."); //record to log
                verticalStage.MoveCheckInterlock(120000);
                #region Rotate to Connect
                for (int i = 0; i < 1; i++) //grind for 1 cycles
                {
                    direction = "forward"; //grinding direction set to forward
                    rotator.SetMotorRPM(RPM, direction); //grind at set speed and direction until stop command sent
                    Thread.Sleep(4000); //Sleep for grind //KC: was 4000
                    rotator.MotorStop(); //Stop grinding motor

                    //direction = "reverse"; //grinding direction set to reverse
                    //rotator.SetMotorRPM(RPM, direction); //grind at set speed and direction until stop command sent
                    //Thread.Sleep(4000); //Sleep for grind //KC: was 4000
                    //rotator.MotorStop(); //Stop grinding motor
                }
                #endregion
                #endregion

                #region D. Gross Find Bottom

                #region OneStep
                myStep = bottom + 1000; //should equal close to the bottom calculated in the previous step
                verticalStage.MoveToWaitDone(myStep);
                #endregion

                #endregion

                #region E. Move Stepper Back Up
                ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState());
                ScriptLog(Severity.Info, "Moving Stepper to Position 0");
                verticalStage.MoveToWaitDone(-500);
                ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState());
                #endregion

                #region F. Creep Loop 2
                f = 1;
                while (verticalStage.GetFlagState() == "0")
                {
                    //ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState());
                    verticalStage.MoveToWaitDone(-500 + (100 * f));
                    f++;
                }
                steploss2 = verticalStage.GetPosition();
                ScriptLog(Severity.Info, "New steps to uncover home sensor:" + steploss2); //record to log
                #endregion

                #region G. Calculate Gross Offset
                overallLoss = Convert.ToInt32(steploss2) - Convert.ToInt32(steploss);
                bottom = myStep - overallLoss;
                ScriptLog(Severity.Info, "New bottom is around:" + bottom);
                #endregion

                ScriptLog(Severity.Info, "Offset Measuring Part II Complete.");
                #endregion

                #region 2. Offset Finding Part III

                ScriptLog(Severity.Info, "Determining Primary Offset");

                #region A. Initialize Stepper
                ScriptLog(Severity.Info, "Initializing Stepper..."); //record to log
                verticalStage.Initialize();

                //ScriptLog(Severity.Info, "Initializing Syringe and Valves..."); //record to log
                //myCavro.Initialize(3);
                #endregion

                #region B. Creep Loop
                verticalStage.MoveToWaitDone(-500);
                f = 1;
                while (verticalStage.GetFlagState() == "0")
                {
                    //ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState());
                    verticalStage.MoveToWaitDone(-500 + (10 * f));
                    f++;
                }
                steploss = verticalStage.GetPosition();
                ScriptLog(Severity.Info, "Steps to uncover home sensor:" + steploss); //record to log
                #endregion

                #region C. Connect to Cap
                ScriptLog(Severity.Info, "Connecting to Grinder Cap..."); //record to log
                verticalStage.MoveCheckInterlock(120000);
                #region Rotate to Connect
                for (int i = 0; i < 1; i++) //grind for 1 cycles
                {
                    direction = "forward"; //grinding direction set to forward
                    rotator.SetMotorRPM(RPM, direction); //grind at set speed and direction until stop command sent
                    Thread.Sleep(4000); //Sleep for grind //KC: was 4000
                    rotator.MotorStop(); //Stop grinding motor

                    //direction = "reverse"; //grinding direction set to reverse
                    //rotator.SetMotorRPM(RPM, direction); //grind at set speed and direction until stop command sent
                    //Thread.Sleep(4000); //Sleep for grind //KC: was 4000
                    //rotator.MotorStop(); //Stop grinding motor
                }
                #endregion
                #endregion

                #region D. Gross Find Bottom

                    #region OneStep
                    myStep = bottom + 1000; //should equal close to the bottom calculated in the previous step
                    verticalStage.MoveToWaitDone(myStep);
                    #endregion

                #endregion

                #region E. Move Stepper Back Up
                ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState());
                ScriptLog(Severity.Info, "Moving Stepper to Position 0");
                verticalStage.MoveToWaitDone(-500);
                ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState());
                #endregion

                #region F. Creep Loop 2
                f = 1;
                while (verticalStage.GetFlagState() == "0")
                {
                    //ScriptLog(Severity.Info, "Flag state is:" + verticalStage.GetFlagState());
                    verticalStage.MoveToWaitDone(-500 + (10 * f));
                    f++;
                }
                steploss2 = verticalStage.GetPosition();
                ScriptLog(Severity.Info, "New steps to uncover home sensor:" + steploss2); //record to log
                #endregion

                #region G. Calculate Gross Offset
                overallLoss = Convert.ToInt32(steploss2) - Convert.ToInt32(steploss);
                bottom = myStep - overallLoss;
                int newoffset = bottom - 276000;
                ScriptLog(Severity.Info, "New Offset is:" + newoffset);
                #endregion

                ScriptLog(Severity.Info, "Offset Measuring Part III Complete.");
                #endregion

                #region 4. Offset Finding Part IV

                ScriptLog(Severity.Info, "Determining Final Offset");

                #region Variables
                int encoderPos1;
                int encoderPos2;
                int encDistance;
                int encGoalFor;
                int encGoalRev;
                int encGoalAvg;
                double encThresholdPercent = 0.9; //was 0.7
                int encThreshold;


                int stepSize = 200;
                int stepAmnt = 30;
                int totSteps = (2 * stepAmnt) + 1; //# steps before target offset, target offset, and # steps after target offset
                int startStep = (bottom - (stepAmnt * stepSize));
                int step = startStep;
                bool alreadyStalled = false;

                //int stepnum = (2 * (3000 / stepSize)) + 1;

                List<string> grindprofile = new List<string>();
                List<int> stallsteps = new List<int>();
                string filename = DateTime.Now.ToString("yyyy_MM_dd_HHmmss");
                #endregion

                #region A. Reinitialize Stepper, RoboClaw
                ScriptLog(Severity.Info, "Re-initializing Stepper");
                verticalStage.Initialize();
                #endregion

                #region B. Refine Offset
                ScriptLog(Severity.Info, "Rotation Offset Test");
                encoderPos1 = rotator.ReadEncoderPosition();
                ScriptLog(Severity.Info, "Starting Encoder Position = " + encoderPos1); //record to log
                int timesStalled = 0;

                for (int j = 1; j <= 1; j++) //to go from 25-195 needs to be 17
                {
                    int logStep = 0;

                    #region Setup Logging
                    grindprofile.Add("Constant Velocity Disabled," + RPM.ToString());
                    grindprofile.Add("Step #, Step, Rot. Dir, Rot. Dist");
                    #endregion

                    #region Initial Rotation

                    //rotate forward with constant velocity for 4 seconds
                    direction = "forward"; //grinding direction set to forward
                    rotator.SetMotorRPM(RPM, direction); //grind at set speed and direction until stop command sent
                    Thread.Sleep(4000); //Sleep for grind //KC: was 4000
                    rotator.MotorStop(); //Stop grinding motor

                    //calculate distance traveled forward
                    encoderPos2 = rotator.ReadEncoderPosition();
                    encGoalFor = encoderPos2 - encoderPos1;
                    grindprofile.Add(logStep + ", Free Rotation , " + direction + ", " + (encGoalFor));
                    ScriptLog(Severity.Info, "Travel = " + (encGoalFor)); //record to log

                    //rotate reverse with constant velocity for 4 seconds
                    direction = "reverse"; //grinding direction set to reverse
                    rotator.SetMotorRPM(RPM, direction); //grind at set speed and direction until stop command sent
                    Thread.Sleep(4000); //Sleep for grind //KC: was 4000
                    rotator.MotorStop(); //Stop grinding motor

                    //calculate distance traveled reverse
                    encoderPos1 = rotator.ReadEncoderPosition();
                    encGoalRev = encoderPos2 - encoderPos1;
                    grindprofile.Add(" , , " + direction + ", " + (encGoalRev));
                    ScriptLog(Severity.Info, "Travel = " + (encGoalRev)); //record to log
                    logStep++;

                    //calculate min encoder value allowed
                    encGoalAvg = (encGoalFor + encGoalRev) / 2;
                    encThreshold = Convert.ToInt32(encGoalAvg * encThresholdPercent);
                    ScriptLog(Severity.Info, "Min Encoder Value Allowed : " + encThreshold); //record to log
                    #endregion

                    #region Connect to Cap

                    ScriptLog(Severity.Info, "Connecting to Grinder Cap..."); //record to log
                    verticalStage.MoveCheckInterlock(120000);

                    direction = "forward"; //grinding direction set to forward
                    rotator.SetMotorRPM(RPM, direction);
                    Thread.Sleep(4000); //Sleep for grind //KC: was 4000
                    rotator.MotorStop(); //Stop grinding motor

                    encoderPos2 = rotator.ReadEncoderPosition();
                    encDistance = encoderPos2 - encoderPos1;
                    if (encDistance < (encThreshold))
                    {
                        ScriptLog(Severity.Info, "Grinder stalled at step: " + (210000 + offset)); //record to log
                        stallsteps.Add(210000 + offset);
                    }
                    grindprofile.Add(logStep + " , Cap Engaged , " + direction + ", " + (encDistance));
                    ScriptLog(Severity.Info, "Travel = " + (encDistance)); //record to log

                    direction = "reverse"; //grinding direction set to reverse
                    rotator.SetMotorRPM(RPM, direction);
                    Thread.Sleep(4000); //Sleep for grind //KC: was 4000
                    rotator.MotorStop(); //Stop grinding motor

                    encoderPos1 = rotator.ReadEncoderPosition();
                    encDistance = encoderPos2 - encoderPos1;
                    if (encDistance < (encThreshold))
                    {
                        ScriptLog(Severity.Info, "Grinder stalled at step: " + (210000 + offset)); //record to log
                        stallsteps.Add(210000 + offset);
                    }
                    grindprofile.Add(" , , " + direction + ", " + (encoderPos2 - encoderPos1));
                    ScriptLog(Severity.Info, "Travel = " + (encoderPos2 - encoderPos1)); //record to log
                    logStep++;
                    #endregion

                    #region Rotate Steps
                    ScriptLog(Severity.Info, "Grinding..."); //record to log
                    for (int i = 0; i < totSteps && timesStalled < 2; i++)
                    {
                        //Move stepper to specified step
                        ScriptLog(Severity.Info, "Step " + (i + 1) + " of " + totSteps); //record to log
                        ScriptLog(Severity.Info, "Moving to step " + step); //record to log
                        verticalStage.MoveToWaitDone(step);

                        //Rotate forward at constant power for 4 seconds
                        direction = "forward"; //grinding direction set to forward
                        rotator.SetMotorRPM(RPM, direction); //grind at set speed and direction until stop command sent
                        Thread.Sleep(4000); //Sleep for grind //KC: was 4000
                        rotator.MotorStop(); //Stop grinding motor

                        //check distance rotated against threshold limit
                        encoderPos2 = rotator.ReadEncoderPosition();
                        encDistance = encoderPos2 - encoderPos1;
                        if (encDistance < encThreshold)
                        {
                            ScriptLog(Severity.Info, "Grinder at bottom at step: " + (step)); //record to log
                            stallsteps.Add(step);
                            timesStalled++;
                        }
                        ScriptLog(Severity.Info, "Travel = " + (encoderPos2 - encoderPos1)); //record to log
                        grindprofile.Add(logStep + ", " + step + ", " + direction + ", " + (encoderPos2 - encoderPos1));

                        //Rotate reverse at constant power for 4 seconds
                        direction = "reverse"; //grinding direction set to reverse
                        rotator.SetMotorRPM(RPM, direction); //grind at set speed and direction until stop command sent
                        Thread.Sleep(4000); //Sleep for grind //KC: was 4000
                        rotator.MotorStop(); //Stop grinding motor

                        //check distance rotated against threshold limit
                        encoderPos1 = rotator.ReadEncoderPosition();
                        encDistance = encoderPos2 - encoderPos1;
                        if (encDistance < encThreshold)
                        {
                            ScriptLog(Severity.Info, "Grinder at bottom at step: " + (step)); //record to log
                            stallsteps.Add(step);
                            timesStalled++;
                        }
                        ScriptLog(Severity.Info, "Travel = " + (encoderPos2 - encoderPos1)); //record to log
                        grindprofile.Add(" , , " + direction + ", " + (encoderPos2 - encoderPos1));

                        step = step + stepSize;
                    }

                    #endregion
                }
                #endregion

                #region C. Calculate Refined Offset
                if (stallsteps.Count() >= 1)
                {
                    int stalledpoint = stallsteps[0];
                    ScriptLog(Severity.Info, "Stalled at Step " + stalledpoint);

                    offset = stalledpoint - 276000;

                    ScriptLog(Severity.Info, "Final Offset = " + offset);
                }
                #endregion

                ScriptLog(Severity.Info, "Offset Measuring Part IV Complete.");
                #endregion

                #region 5. Change Offset
                ScriptLog(Severity.Info, "Recording New Offset");
                myParentScript.ChangeOffset(offset);
                #endregion

                #region 6. End Calibration
                ScriptLog(Severity.Info, "Homing Z-Axis for Cartridge Removal");
                verticalStage.Initialize();
                #endregion

                //FIXTHIS
                if (timesStalled < 2)
                {
                    myParentScript.cusMsgBox2.Show("Calibration Unsuccessful.", "Calibration Complete", MessageBoxButtons.OK);
                }
                else
                {
                    myParentScript.cusMsgBox2.Show("Calibration Successful. VertOffset = " + grinderVertOffset, "Calibration Complete", MessageBoxButtons.OK);
                }
                
                //MessageBox.Show("Calibration Successful.", "Calibration Complete", MessageBoxButtons.OK);

                ScriptLog(Severity.Info, "Calibration Complete.");
            }

            #endregion

            #region Recording Functions

            public void RecordData(int currentState, int nextState)
            {
                //check if we are leaving the cooling, heating or running state so we can record temperature data
                int[] tempStates = { 2, 3, 4, 5 };
                int[] motorStates = { 4, 5, 12, 13 };
                if (tempStates.Contains(currentState))
                {
                    if (nextState != 5)
                    {
                        tempBlock.RecordTempData();
                        ScriptLog(Severity.Control, "Recording Temperature Data");
                    }
                }
                if (motorStates.Contains(currentState))
                {
                    if (nextState != 5)
                    {
                        RecordMotorData();
                        ScriptLog(Severity.Control, "Recording Motor Data");
                    }
                }

            }

            public void RecordMotorData()
            {
                //convert list to string
                string motorLog = string.Join("", motorData);
                //record string to file
                System.IO.File.AppendAllText(motorFileName, motorLog);
                //clear list of temp data and set tempNum back to 0 to start over
                motorData.Clear();
                motorNum = 0;
            }

            public void StartMotorLogging(int nextState)
            {
                int[] motorStates = { 4, 12, 13 };
                if (motorStates.Contains(nextState))
                {
                    ScriptLog(Severity.Control, "Starting Motor Logging");
                    //create file name
                    motorFileSuffix = @"_" + DateTime.Now.ToString("yyyy_MM_dd") + "_MotorLog.csv";
                    motorFileName = @motorFolderName + motorScriptName + motorFileSuffix;
                    //create folder if one doesn't already exist
                    System.IO.Directory.CreateDirectory(motorFolderName);
                    //create file if one doesn't already exist
                    if (!System.IO.File.Exists(motorFileName))
                    {
                        System.IO.File.Create(motorFileName).Close();
                        //make header and save to file
                        System.IO.File.AppendAllText(motorFileName,"#,Date/Time,Stepper Position, Z-Axis Offset, Rot Direction, Encoder Travel\n");
                    }
                }
            }

            public void UpdateTempDataInsState(int nextState)
            {
                if (nextState == 2)
                {
                    tempBlock.tempData.Add("Cooling\n");
                }
                else if (nextState == 3)
                {
                    tempBlock.tempData.Add("Heating\n");
                }
                else if (nextState == 4)
                {
                    tempBlock.tempData.Add("Running Protocol\n");
                }
            }

            #endregion

            #region Currently Unused Functions

            public void Prime(string runType)
            {
                ScriptLog(Severity.Info, "Priming Reagents...");
                if (runType == "Cells")
                {
                    fluidics.PrimeEnz(); //in future may not need to prime
                    fluidics.PrimeBuffer();
                    enzymeLoaded = true;
                }
                else
                {
                    fluidics.PrimeNucIso();
                    fluidics.PrimeNucStor();
                }

                fluidics.PrimeWater();

                fluidics.ClearLines();

            } //using async version instead

            public void SetInsTemp(string temp)
            {
                ScriptLog(Severity.Info, "Setting Instrument Temp");

                bool ambientSensing = false;
                double ambientTemp = 0;
                double startTemp = 70.0;
                double lowerTemp = 41.0;
                if (temp == "4C")
                    tempBlock.ControlTemperature(-10);
                else if (temp == "Room Temp")
                    tempBlock.ControlTemperature(20);
                else
                {
                    if (ambientSensing)
                    {
                        //check ambient temperature
                        ambientTemp = tempBlock.GetTemperature(2);
                        startTemp = startTemp + ((4 / 3) * (24 - ambientTemp));
                        lowerTemp = lowerTemp + ((24 - ambientTemp));
                    }
                    //set starting temperature
                    tempBlock.ControlTemperature(startTemp);
                }
            } //using async version instead

            #endregion
        }
    }
}