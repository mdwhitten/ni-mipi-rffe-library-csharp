using System;
using System.Text;
using System.Collections;
using System.IO;
using NationalInstruments.ModularInstruments.NIDigital;
using NationalInstruments.ReferenceDesignLibraries;
using  NationalInstruments.ReferenceDesignLibraries.DigitalProtocols;

namespace MIPI_RFFE
{
    class Multiple_Command_Programmatic_Burst
    {
        public static void Main()
        {
            string DigitalPath = Path.GetFullPath(@"Digital Project");

            // Load all digital pattern files found in the directory
            var projectFiles = Digital.Utilities.SearchForProjectFiles(DigitalPath, true);

            //Initialize hardware and load pinmap plus sheets into
            //digital pattern instrument. Most of the fucntions below are
            //a lightweight wrapper around NI - Digital functions.

            //If you change the insturment name below, you must also change the instrument name
            //in the PinMap file
            NIDigital digital = new NIDigital("PXIe-6570", false, false, "");

            // Download all the located files to the instrument
            Digital.LoadProjectFiles(digital, projectFiles);

            //Turn RFFE bus power on
            digital.PinAndChannelMap.GetPinSet("RFFEVIO").WriteStatic(PinState._1);

            //Setup new register data to send to register 0
            RegisterData regData = new RegisterData {
                SlaveAddress = 0xF, //15
                WriteRegisterData = new byte[1] { 0x8 },
                ByteCount = 1
            };

            MipiRffeCommand command = MipiRffe.CreateCommand(CommandType.Reg0Write, regData);

            //Trgger type is set to none so burst will start immediately
            var triggerConfig = new Digital.TriggerConfiguration {
                BurstTriggerType = TriggerType.None,
            };

            command.Burst(digital, "RFFEDATA", triggerConfig);

            Console.ReadKey();

            Digital.DisconnectAndClose(digital);
        }

    }
}
