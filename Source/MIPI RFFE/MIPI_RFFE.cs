using System;
using System.Collections.Generic;
using System.Text;
using static NationalInstruments.ReferenceDesignLibraries.Digital;
using NationalInstruments.ModularInstruments.NIDigital;

namespace NationalInstruments.ReferenceDesignLibraries.DigitalProtocols
{
    #region Type Definitions
    public struct RegisterData
    {
        public byte SlaveAddress;
        public ushort RegisterAddress;
        public byte[] WriteRegisterData;
        public byte ByteCount;
    }
    public struct ReadData
    {
        public string[] Register;
        public byte[] Data;
    }
    public enum CommandType
    {
        Reg0Write
    }
    #endregion
    /// <summary>
    /// A factory class for creating MIPI RFFE command objects.
    /// </summary>
    public static class MipiRffe
    {
        public static MipiRffeCommand CreateCommand(CommandType type, RegisterData registerData)
        {
            switch (type)
            {
                case CommandType.Reg0Write:
                    return new Reg0Write(registerData);
                default:
                    throw new NotImplementedException("This command has not been implemented.");
            }
        }
    }
    /// <summary>
    /// Defines a MIPI RFFE command.
    /// </summary>
    public abstract class MipiRffeCommand
    {
        protected RegisterData registerData;
        protected uint[] sourceData;
        protected int byteCount;
        #region Defined Properties & Methods

        protected MipiRffeCommand(RegisterData regData) { }

        /// <summary>
        /// Validates that the register data supplied to the object is valid.
        /// </summary>
        public void ValidateLogic()
        {
            if (registerData.SlaveAddress < 0x0 || registerData.SlaveAddress >= 0x10)
            {
                throw new ArgumentOutOfRangeException("SlaveAddress", registerData.SlaveAddress.ToString("X"), "Slave Address out of range");
            }
            if (registerData.RegisterAddress < 0x0 || registerData.RegisterAddress > UpperAddressLimit)
            {
                throw new ArgumentOutOfRangeException("RegisterAddress", registerData.SlaveAddress.ToString("X"),
                    "Register Address out of range. Check that the address is valid based on the selected command.");
            }
            if (registerData.ByteCount <= 0 || registerData.ByteCount > ByteCountLimit)
            {
                throw new ArgumentOutOfRangeException("ByteCount", registerData.ByteCount, "Byte Count out of range");
            }
        }

        /// <summary>
        /// Bursts the RFFE MIPI command on the digital pattern instrument.
        /// </summary>
        /// <param name="niDigital">Specifies the NI Digital instrument session to use for bursting the pattern.</param>
        /// <param name="pinName">Specifies the pin name to use for sourcing and capturing data.</param>
        /// <param name="triggerConfig">Specifies the triggering configuration to use for the pattern generation.</param>
        /// <returns></returns>
        public ReadData Burst(NIDigital niDigital, string pinName, TriggerConfiguration triggerConfig)
        {
            //Create source and capture waveforms in driver
            CreateDigitalWaveforms(niDigital, pinName);

            //Create dynamic source waveform data for selected command
            niDigital.SourceWaveforms.WriteBroadcast(SourceName, sourceData);

            //reg0 set based on amount of bytes used
            niDigital.PatternControl.WriteSequencerRegister("reg0", byteCount);

            //Burst pattern based on the input trigger settings
            Digital.InitiatePatternGeneration(niDigital, SourceName, triggerConfig);

            //On read calls only, return capture data. TODO - still needs to be implemented.
            return new ReadData();
        }

        #endregion
        /// <summary>
        /// Creates the source and capture waveforms used by the instrument.
        /// </summary>
        /// <param name="niDigital">Specifies the NI-Digital instrument.</param>
        /// <param name="pinName">Specifies the pin name to source to and capture from.</param>
        private void CreateDigitalWaveforms(NIDigital niDigital, string pinName)
        {
            //Create 1 bit sample width source and 8 bit sample width capture waveforms,
            //using appropriate name for the command.
            niDigital.SourceWaveforms.CreateSerial(pinName, SourceName,
                SourceDataMapping.Broadcast, 1, BitOrder.MostSignificantBitFirst);
            niDigital.CaptureWaveforms.CreateSerial(pinName, SourceName,
                8, BitOrder.MostSignificantBitFirst);
        }


        #region Abstract Properties & Methods
        protected abstract int ByteCountLimit { get; }
        protected abstract int UpperAddressLimit { get; }
        public abstract string SourceName { get; }
        #endregion

        #region Static Utility Functions
        /// <summary>
        /// Converts a bit string into an array of bits.
        /// </summary>
        /// <param name="bitString">The bit string to transform.</param>
        /// <returns>An array of bits.</returns>
        public static uint[] BitStringToArray(string bitString)
        {
            char[] charArray = bitString.ToCharArray();
            uint[] bitArray = new uint[charArray.Length];

            for (int i = 0; i < charArray.Length; i++)
            {
                bitArray[i] = (uint)char.GetNumericValue(charArray[i]);
            }
            return bitArray;
        }
        /// <summary>
        /// Calculates the parity based on the incoming bitstream.
        /// </summary>
        /// <param name="bitString">Specifies the bit string to calculate the parity on.</param>
        /// <returns>A parity bit based upon the calculation.</returns>
        public static string CalculateParity(string bitString)
        {
            uint[] bitArray = BitStringToArray(bitString);
            uint sum = 0;
            for (int i = 0; i < bitArray.Length; i++) sum += bitArray[i];

            //Even 1's is a 1
            //Odd 1's is a 0 to achieve odd parity
            if (sum % 2 == 0) return "1";
            else return "0";
        }
        #endregion
    }
    class Reg0Write : MipiRffeCommand
    {
        protected override int ByteCountLimit => 1;
        protected override int UpperAddressLimit => 0xFFFF;
        public override string SourceName => "Reg0Write";
        
        /// <summary>
        /// Initializes the command by validating the data and creating the bit stream for the command.
        /// </summary>
        /// <param name="regData">The register data to be written from the instrument.</param>
        public Reg0Write(RegisterData regData) : base(regData)
        {
            registerData = regData;
            ValidateLogic();

            //For Reg0Write, build 4 bit SA and 7 bit Data with parity.
            //COMMAND/ADDRESS/DATA FRAME

            string slaveAddress = Convert.ToString(registerData.SlaveAddress, 2).PadLeft(4, '0');
            //Only take the first element of register data
            string data = Convert.ToString(registerData.WriteRegisterData[0], 2).PadLeft(7, '0');

            //Insert a 1 between the slave and data for the command bit
            //This is implemented in the pattern so is only used here for parity calculation
            string parityString = string.Concat(slaveAddress, "1", data);
            string parityBit = CalculateParity(parityString);

            string finalDataString = string.Concat(slaveAddress, data, parityBit);

            byteCount = 1;
            sourceData = BitStringToArray(finalDataString);
        }

    }
}
