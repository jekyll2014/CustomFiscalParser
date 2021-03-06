﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using CustomFiscalParser.Properties;

namespace CustomFiscalParser
{
    public class ParseEscPos
    {
        //INTERFACES
        //source of the data to parce
        //public static string sourceData = ""; //in Init()
        public static List<byte> sourceData = new List<byte>(); //in Init()

        //source of the command description (DataTable)
        public static DataTable commandDataBase = new DataTable(); //in Init()

        //INTERNAL VARIABLES
        //Command list preselected
        //private static Dictionary<int, string> _commandList = new Dictionary<int, string>(); //in Init()

        private const byte ackSign = 0x06;
        private const byte nackSign = 0x15;
        private const byte frameStartSign = 0x01;
        public static bool itIsReply;
        public static bool itIsReplyNACK;
        public static bool crcFailed;
        public static bool lengthIncorrect;

        //RESULT VALUES
        public static int commandFrameLength;

        //place of the frame start in the text
        public static int commandFramePosition; //in findCommand()

        //Command text
        public static string commandName; //in findCommand()

        //Command desc
        public static string commandDesc; //in findCommand()

        //string number of the command found
        public static int commandDbLineNum; //in findCommand()

        //height of the command
        public static int commandDbHeight; //in findCommand()

        //string number of the command found
        public static List<int> commandParamDbLineNum = new List<int>(); //in findCommand()

        //list of command parameters real sizes
        public static List<int> commandParamSize = new List<int>(); //in findCommand()

        //list of command parameters sizes defined in the database
        public static List<string> commandParamSizeDefined = new List<string>(); //in findCommand()

        //command parameter description
        public static List<string> commandParamDesc = new List<string>(); //in findCommand()

        //command parameter type
        public static List<string> commandParamType = new List<string>(); //in findCommand()

        //command parameter RAW value
        public static List<List<byte>> commandParamRAWValue = new List<List<byte>>(); //in findCommand()

        //command parameter value
        public static List<string> commandParamValue = new List<string>(); //in findCommand()

        //Length of command+parameters text
        public static int commandBlockLength;

        public class CSVColumns
        {
            public static int CommandName { get; set; } = 0;
            public static int CommandParameterSize { get; set; } = 1;
            public static int CommandParameterType { get; set; } = 2;
            public static int CommandParameterValue { get; set; } = 3;
            public static int CommandDescription { get; set; } = 4;
            public static int ReplyParameterSize { get; set; } = 5;
            public static int ReplyParameterType { get; set; } = 6;
            public static int ReplyParameterValue { get; set; } = 7;
            public static int ReplyDescription { get; set; } = 8;
        }

        public class DataTypes
        {
            public static string Password { get; set; } = "password";
            public static string String { get; set; } = "string";
            public static string Number { get; set; } = "number";
            public static string Money { get; set; } = "money";
            public static string Quantity { get; set; } = "quantity";
            public static string Error { get; set; } = "error#";
            public static string Data { get; set; } = "data";
            public static string PrefData { get; set; } = "prefdata";
            public static string TLVData { get; set; } = "tlvdata";
            public static string Bitfield { get; set; } = "bitfield";
        }


        //lineNum = -1 - искать во всех командах
        //lineNum = x - искать в команде на определенной стоке базы
        public static bool FindCommand(int _pos, int lineNum = -1)
        {
            //reset all result values
            ClearCommand();

            if (sourceData.Count < _pos + 1) return false;
            //check if it's a command or reply
            if (sourceData[_pos] == frameStartSign)
            {
                CSVColumns.CommandParameterSize = 1;
                CSVColumns.CommandParameterType = 2;
                CSVColumns.CommandParameterValue = 3;
                CSVColumns.CommandDescription = 4;
                itIsReply = false;
                if (sourceData.Count < _pos + 3) return false;
            }
            else if (sourceData[_pos] == ackSign || sourceData[_pos] == nackSign)
            {
                itIsReply = true;
                if (sourceData[_pos] == nackSign) itIsReplyNACK = true;
                CSVColumns.CommandParameterSize = 5;
                CSVColumns.CommandParameterType = 6;
                CSVColumns.CommandParameterValue = 7;
                CSVColumns.CommandDescription = 8;
                if (sourceData.Count < _pos + 4) return false;
                _pos++;
            }
            else
            {
                return false;
            }

            //select data frame
            commandFrameLength = 0;
            if (sourceData[_pos] == frameStartSign)
            {
                commandFrameLength = sourceData[_pos + 1];
                commandFrameLength = commandFrameLength + sourceData[_pos + 2] * 256;
                _pos += 3;
            }
            else
            {
                return false;
            }

            //check if "commandFrameLength" less than "sourcedata". note the last byte of "sourcedata" is CRC.
            if (sourceData.Count < _pos + commandFrameLength + 1)
            {
                commandFrameLength = sourceData.Count - _pos;
                lengthIncorrect = true;
            }

            //find command
            var i = 0;
            if (lineNum != -1) i = lineNum;
            if (sourceData.Count < _pos + 1) return false; //check if it doesn't go over the last symbol
            for (; i < commandDataBase.Rows.Count; i++)
                if (commandDataBase.Rows[i][CSVColumns.CommandName].ToString() != "")
                    if (sourceData[_pos] ==
                        Accessory.ConvertHexToByte(commandDataBase.Rows[i][CSVColumns.CommandName].ToString())
                    ) //if command matches
                        if (lineNum < 0 || lineNum == i) //if string matches
                        {
                            commandName = commandDataBase.Rows[i][CSVColumns.CommandName].ToString();
                            commandDbLineNum = i;
                            commandDesc = commandDataBase.Rows[i][CSVColumns.CommandDescription].ToString();
                            commandFramePosition = _pos;
                            //get CRC of the frame
                            //check length of sourceData
                            int calculatedCRC =
                                Q3xf_CRC(sourceData.GetRange(_pos - 2, commandFrameLength + 2).ToArray(),
                                    commandFrameLength + 2);
                            int sentCRC = sourceData[_pos + commandFrameLength];
                            if (calculatedCRC != sentCRC) crcFailed = true;
                            else crcFailed = false;
                            //check command height - how many rows are occupated
                            var i1 = 0;
                            while (commandDbLineNum + i1 + 1 < commandDataBase.Rows.Count &&
                                   commandDataBase.Rows[commandDbLineNum + i1 + 1][CSVColumns.CommandName].ToString() ==
                                   "") i1++;
                            commandDbHeight = i1;
                            return true;
                        }

            return false;
        }

        public static bool FindCommandParameter()
        {
            ClearCommandParameters();
            //collect parameters from database
            var _stopSearch = commandDbLineNum + 1;
            while (_stopSearch < commandDataBase.Rows.Count &&
                   commandDataBase.Rows[_stopSearch][CSVColumns.CommandName].ToString() == "") _stopSearch++;
            for (var i = commandDbLineNum + 1; i < _stopSearch; i++)
                if (commandDataBase.Rows[i][CSVColumns.CommandParameterSize].ToString() != "")
                {
                    commandParamDbLineNum.Add(i);
                    commandParamSizeDefined.Add(commandDataBase.Rows[i][CSVColumns.CommandParameterSize].ToString());
                    if (commandParamSizeDefined.Last() == "?")
                    {
                        commandParamSize.Add(commandFrameLength - 1);
                        for (var i1 = 0; i1 < commandParamSize.Count - 1; i1++)
                            commandParamSize[commandParamSize.Count - 1] -= commandParamSize[i1];
                        if (commandParamSize[commandParamSize.Count - 1] < 0)
                            commandParamSize[commandParamSize.Count - 1] = 0;
                    }
                    else
                    {
                        var v = 0;
                        int.TryParse(commandParamSizeDefined.Last(), out v);
                        commandParamSize.Add(v);
                    }

                    commandParamDesc.Add(commandDataBase.Rows[i][CSVColumns.CommandDescription].ToString());
                    commandParamType.Add(commandDataBase.Rows[i][CSVColumns.CommandParameterType].ToString());
                }

            var commandParamPosition = commandFramePosition + 1;
            //process each parameter
            for (var parameter = 0; parameter < commandParamDbLineNum.Count; parameter++)
            {
                //collect predefined RAW values
                var predefinedParamsRaw = new List<string>();
                var j = commandParamDbLineNum[parameter] + 1;
                while (j < commandDataBase.Rows.Count &&
                       commandDataBase.Rows[j][CSVColumns.CommandParameterValue].ToString() != "")
                {
                    predefinedParamsRaw.Add(commandDataBase.Rows[j][CSVColumns.CommandParameterValue].ToString());
                    j++;
                }

                //Calculate predefined params
                var predefinedParamsVal = new List<int>();
                foreach (var formula in predefinedParamsRaw)
                {
                    var val = 0;
                    if (!int.TryParse(formula.Trim(), out val)) val = 0;
                    predefinedParamsVal.Add(val);
                }

                //get parameter from text
                var errFlag = false; //Error in parameter found
                var errMessage = "";

                var _prmType = commandDataBase.Rows[commandParamDbLineNum[parameter]][CSVColumns.CommandParameterType]
                    .ToString().ToLower();
                if (parameter != 0) commandParamPosition = commandParamPosition + commandParamSize[parameter - 1];
                var _raw = new List<byte>();
                var _val = "";

                if (_prmType == DataTypes.Password)
                {
                    double l = 0;
                    if (commandParamPosition + commandParamSize[parameter] <= sourceData.Count - 1)
                    {
                        _raw = sourceData.GetRange(commandParamPosition, commandParamSize[parameter]);
                        l = RawToPassword(_raw.ToArray());
                        _val = l.ToString().Substring(0, 2) + "/" + l.ToString().Substring(2);
                    }
                    else
                    {
                        errFlag = true;
                        errMessage = "!!!ERR: Out of data bound!!!";
                        if (commandParamPosition <= sourceData.Count - 1)
                            _raw = sourceData.GetRange(commandParamPosition,
                                sourceData.Count - 1 - commandParamPosition);
                    }
                }
                else if (_prmType == DataTypes.String)
                {
                    if (commandParamPosition + commandParamSize[parameter] <= sourceData.Count - 1)
                    {
                        _raw = sourceData.GetRange(commandParamPosition, commandParamSize[parameter]);
                        _val = RawToString(_raw.ToArray(), commandParamSize[parameter]);
                    }
                    else
                    {
                        errFlag = true;
                        errMessage = "!!!ERR: Out of data bound!!!";
                        if (commandParamPosition <= sourceData.Count - 1)
                            _raw = sourceData.GetRange(commandParamPosition,
                                sourceData.Count - 1 - commandParamPosition);
                    }
                }
                else if (_prmType == DataTypes.Number)
                {
                    double l = 0;
                    if (commandParamPosition + commandParamSize[parameter] <= sourceData.Count - 1)
                    {
                        _raw = sourceData.GetRange(commandParamPosition, commandParamSize[parameter]);
                        l = RawToNumber(_raw.ToArray());
                        _val = l.ToString();
                    }
                    else
                    {
                        errFlag = true;
                        errMessage = "!!!ERR: Out of data bound!!!";
                        if (commandParamPosition <= sourceData.Count - 1)
                            _raw = sourceData.GetRange(commandParamPosition,
                                sourceData.Count - 1 - commandParamPosition);
                    }
                }
                else if (_prmType == DataTypes.Money)
                {
                    double l = 0;
                    if (commandParamPosition + commandParamSize[parameter] <= sourceData.Count - 1)
                    {
                        _raw = sourceData.GetRange(commandParamPosition, commandParamSize[parameter]);
                        l = RawToMoney(_raw.ToArray());
                        _val = l.ToString();
                    }
                    else
                    {
                        errFlag = true;
                        errMessage = "!!!ERR: Out of data bound!!!";
                        if (commandParamPosition <= sourceData.Count - 1)
                            _raw = sourceData.GetRange(commandParamPosition,
                                sourceData.Count - 1 - commandParamPosition);
                    }
                }
                else if (_prmType == DataTypes.Quantity)
                {
                    double l = 0;
                    if (commandParamPosition + commandParamSize[parameter] <= sourceData.Count - 1)
                    {
                        _raw = sourceData.GetRange(commandParamPosition, commandParamSize[parameter]);
                        l = RawToQuantity(_raw.ToArray());
                        _val = l.ToString();
                    }
                    else
                    {
                        errFlag = true;
                        errMessage = "!!!ERR: Out of data bound!!!";
                        if (commandParamPosition <= sourceData.Count - 1)
                            _raw = sourceData.GetRange(commandParamPosition,
                                sourceData.Count - 1 - commandParamPosition);
                    }
                }
                else if (_prmType == DataTypes.Error)
                {
                    double l = 0;
                    if (commandParamPosition + commandParamSize[parameter] <= sourceData.Count - 1)
                    {
                        _raw = sourceData.GetRange(commandParamPosition, commandParamSize[parameter]);
                        l = RawToError(_raw.ToArray());
                        _val = l.ToString();
                        if (l != 0 && commandFrameLength == 3 && parameter == 0 &&
                            commandParamPosition + commandParamSize[parameter] == sourceData.Count - 1)
                        {
                            if (commandParamDbLineNum.Count > 1)
                                commandParamDbLineNum.RemoveRange(1, commandParamDbLineNum.Count - parameter - 1);
                            if (commandParamSize.Count > 1)
                                commandParamSize.RemoveRange(1, commandParamSize.Count - parameter - 1);
                            if (commandParamSizeDefined.Count > 1)
                                commandParamSizeDefined.RemoveRange(1, commandParamSizeDefined.Count - parameter - 1);
                            if (commandParamDesc.Count > 1)
                                commandParamDesc.RemoveRange(1, commandParamDesc.Count - parameter - 1);
                            if (commandParamType.Count > 1)
                                commandParamType.RemoveRange(1, commandParamType.Count - parameter - 1);
                        }
                    }
                    else
                    {
                        errFlag = true;
                        errMessage = "!!!ERR: Out of data bound!!!";
                        if (commandParamPosition <= sourceData.Count - 1)
                            _raw = sourceData.GetRange(commandParamPosition,
                                sourceData.Count - 1 - commandParamPosition);
                    }
                }
                else if (_prmType == DataTypes.Data)
                {
                    if (commandParamPosition + commandParamSize[parameter] <= sourceData.Count - 1)
                    {
                        _raw = sourceData.GetRange(commandParamPosition, commandParamSize[parameter]);
                        _val = RawToData(_raw.ToArray());
                    }
                    else
                    {
                        errFlag = true;
                        errMessage = "!!!ERR: Out of data bound!!!";
                        if (commandParamPosition <= sourceData.Count - 1)
                            _raw = sourceData.GetRange(commandParamPosition,
                                sourceData.Count - 1 - commandParamPosition);
                    }
                }
                else if (_prmType == DataTypes.PrefData)
                {
                    var prefLength = 0;
                    //get gata length
                    if (commandParamPosition + 2 <= sourceData.Count - 1)
                        prefLength = (int) RawToNumber(sourceData.GetRange(commandParamPosition, 2).ToArray());
                    //check if the size is correct
                    if (prefLength + 2 > commandParamSize[parameter])
                    {
                        prefLength = commandParamSize[parameter] - 2;
                        errFlag = true;
                        errMessage = "!!!ERR: Out of data bound!!!";
                    }
                    else
                    {
                        commandParamSize[parameter] = prefLength + 2;
                    }

                    //get data
                    if (commandParamPosition + commandParamSize[parameter] <= sourceData.Count - 1)
                    {
                        _raw = sourceData.GetRange(commandParamPosition, commandParamSize[parameter]);
                        //_val = "[" + prefLength.ToString() + "]" + Accessory.ConvertHexToString(_raw.Substring(6), CustomFiscalParser.Properties.Settings.Default.CodePage);
                        _val = RawToPrefData(_raw.ToArray(), commandParamSize[parameter]);
                    }
                    else
                    {
                        errFlag = true;
                        errMessage = "!!!ERR: Out of data bound!!!";
                        if (commandParamPosition <= sourceData.Count - 1)
                            _raw = sourceData.GetRange(commandParamPosition,
                                sourceData.Count - 1 - commandParamPosition);
                    }
                }
                else if (_prmType == DataTypes.TLVData)
                {
                    var TlvType = 0;
                    var TlvLength = 0;
                    if (commandParamSize[parameter] > 0)
                    {
                        if (commandParamPosition + 4 <= sourceData.Count - 1)
                        {
                            //get type of parameter
                            TlvType = (int) RawToNumber(sourceData.GetRange(commandParamPosition, 2).ToArray());
                            //get gata length
                            TlvLength = (int) RawToNumber(sourceData.GetRange(commandParamPosition + 2, 2).ToArray());
                        }

                        //check if the size is correct
                        if (TlvLength + 4 > commandParamSize[parameter])
                        {
                            TlvLength = commandParamSize[parameter] - 4;
                            errFlag = true;
                            errMessage = "!!!ERR: Out of data bound!!!";
                        }
                        else
                        {
                            commandParamSize[parameter] = TlvLength + 4;
                        }

                        //get data
                        if (commandParamPosition + commandParamSize[parameter] <= sourceData.Count - 1)
                        {
                            _raw = sourceData.GetRange(commandParamPosition, commandParamSize[parameter]);
                            //_val = "[" + TlvType.ToString() + "]" + "[" + TlvLength.ToString() + "]" + Accessory.ConvertHexToString(_raw.Substring(12), CustomFiscalParser.Properties.Settings.Default.CodePage);
                            _val = RawToTLVData(_raw.ToArray(), commandParamSize[parameter]);
                        }
                        else
                        {
                            errFlag = true;
                            errMessage = "!!!ERR: Out of data bound!!!";
                            if (commandParamPosition <= sourceData.Count - 1)
                                _raw = sourceData.GetRange(commandParamPosition,
                                    sourceData.Count - 1 - commandParamPosition);
                        }
                    }
                }
                else if (_prmType == DataTypes.Bitfield)
                {
                    double l = 0;
                    if (commandParamPosition + commandParamSize[parameter] <= sourceData.Count - 1)
                    {
                        _raw = sourceData.GetRange(commandParamPosition, commandParamSize[parameter]);
                        l = RawToBitfield(_raw[0]);
                        _val = l.ToString();
                    }
                    else
                    {
                        errFlag = true;
                        errMessage = "!!!ERR: Out of data bound!!!";
                        if (commandParamPosition <= sourceData.Count - 1)
                            _raw = sourceData.GetRange(commandParamPosition,
                                sourceData.Count - 1 - commandParamPosition);
                    }
                }
                else
                {
                    //flag = true;
                    errFlag = true;
                    errMessage = "!!!ERR: Incorrect parameter type!!!";
                    if (commandParamPosition + commandParamSize[parameter] <= sourceData.Count - 1)
                    {
                        _raw = sourceData.GetRange(commandParamPosition, commandParamSize[parameter]);
                        //_val = Accessory.ConvertHexToString(_raw, CustomFiscalParser.Properties.Settings.Default.CodePage);
                    }
                    else
                    {
                        errFlag = true;
                        errMessage = "!!!ERR: Out of data bound!!!";
                        if (commandParamPosition <= sourceData.Count - 1)
                            _raw = sourceData.GetRange(commandParamPosition,
                                sourceData.Count - 1 - commandParamPosition);
                    }
                }

                commandParamRAWValue.Add(_raw);
                commandParamValue.Add(_val);

                var predefinedFound =
                    false; //Matching predefined parameter found and it's number is in "predefinedParameterMatched"
                if (errFlag) commandParamDesc[parameter] += errMessage + "\r\n";

                //compare parameter value with predefined values to get proper description
                var predefinedParameterMatched = 0;
                for (var i1 = 0; i1 < predefinedParamsVal.Count; i1++)
                    if (commandParamValue[parameter] == predefinedParamsVal[i1].ToString())
                    {
                        predefinedFound = true;
                        predefinedParameterMatched = i1;
                    }

                commandParamDesc[parameter] += "\r\n";
                if (commandParamDbLineNum[parameter] + predefinedParameterMatched + 1 <
                    commandDbLineNum + commandDbHeight && predefinedFound)
                    commandParamDesc[parameter] +=
                        commandDataBase.Rows[commandParamDbLineNum[parameter] + predefinedParameterMatched + 1][
                            CSVColumns.CommandDescription].ToString();
            }

            ResultLength();
            return true;
        }

        internal static void ClearCommand()
        {
            itIsReply = false;
            itIsReplyNACK = false;
            crcFailed = false;
            lengthIncorrect = false;
            commandFramePosition = -1;
            commandDbLineNum = -1;
            commandDbHeight = -1;
            commandName = "";
            commandDesc = "";

            commandParamSize.Clear();
            commandParamDesc.Clear();
            commandParamType.Clear();
            commandParamValue.Clear();
            commandParamRAWValue.Clear();
            commandParamDbLineNum.Clear();
            commandBlockLength = 0;
        }

        internal static void ClearCommandParameters()
        {
            commandParamSize.Clear();
            commandParamDesc.Clear();
            commandParamType.Clear();
            commandParamValue.Clear();
            commandParamRAWValue.Clear();
            commandParamDbLineNum.Clear();
            commandParamSizeDefined.Clear();
            commandBlockLength = 0;
        }

        internal static int ResultLength() //Calc "CommandBlockLength" - length of command text in source text field
        {
            //FrameStart[1] + DataLength[2] + data + CRC
            commandBlockLength = 3 + commandFrameLength + 1;
            if (itIsReply) commandBlockLength += 3;
            return commandBlockLength;
        }

        public static byte Q3xf_CRC(byte[] data, int length)
        {
            ushort sum = 0;
            for (var i = 0; i < length; i++) sum += data[i];
            var sumH = (byte) (sum / 256);
            var sumL = (byte) (sum - sumH * 256);
            return (byte) (sumH ^ sumL);
        }

        public static string RawToString(byte[] b, int n)
        {
            var outStr = Encoding.GetEncoding(Settings.Default.CodePage).GetString(b);
            if (outStr.Length > n) outStr = outStr.Substring(0, n);
            return outStr;
        }

        public static string RawToPrefData(byte[] b, int n)
        {
            var s = new List<byte>();
            s.AddRange(b);
            if (s.Count < 2) return "";
            if (s.Count > n + 2) s = s.GetRange(0, n + 2);
            var outStr = "";
            var strLength = (int) RawToNumber(s.GetRange(0, 2).ToArray());
            outStr = "[" + strLength + "]";
            if (s.Count == 2 + strLength)
            {
                var b1 = s.GetRange(2, s.Count - 2).ToArray();
                if (Accessory.PrintableByteArray(b1))
                    outStr += "\"" + Encoding.GetEncoding(Settings.Default.CodePage).GetString(b1) + "\"";
                else outStr += "[" + Accessory.ConvertByteArrayToHex(b1) + "]";
            }
            else
            {
                outStr += "INCORRECT LENGTH";
            }

            return outStr;
        }

        // !!! check TLV actual data layout
        public static string RawToTLVData(byte[] b, int n)
        {
            var s = new List<byte>();
            s.AddRange(b);
            if (s.Count < 4) return "";
            if (s.Count > n + 4) s = s.GetRange(0, n + 4);
            var outStr = "";
            var tlvType = (int) RawToNumber(s.GetRange(0, 2).ToArray());
            outStr = "[" + tlvType + "]";
            var strLength = (int) RawToNumber(s.GetRange(2, 2).ToArray());
            outStr += "[" + strLength + "]";
            if (s.Count == 4 + strLength)
            {
                var b1 = s.GetRange(2, s.Count - 2).ToArray();
                if (Accessory.PrintableByteArray(b1))
                    outStr += "\"" + Encoding.GetEncoding(Settings.Default.CodePage)
                        .GetString(s.GetRange(4, s.Count - 4).ToArray()) + "\"";
                else outStr += "[" + Accessory.ConvertByteArrayToHex(b1) + "]";
            }
            else
            {
                outStr += "INCORRECT LENGTH";
            }

            return outStr;
        }

        public static double RawToPassword(byte[] b)
        {
            double l = 0;
            for (var n = 0; n < b.Length; n++) l += b[n] * Math.Pow(256, n);
            return l;
        }

        public static double RawToNumber(byte[] b)
        {
            double l = 0;
            for (var n = 0; n < b.Length; n++)
                if (n == b.Length - 1 && Accessory.GetBit(b[n], 7))
                {
                    l += b[n] * Math.Pow(256, n);
                    l = l - Math.Pow(2, b.Length * 8);
                }
                else
                {
                    l += b[n] * Math.Pow(256, n);
                }

            return l;
        }

        public static double RawToMoney(byte[] b)
        {
            double l = 0;
            for (var n = 0; n < b.Length; n++)
                if (n == b.Length - 1 && Accessory.GetBit(b[n], 7))
                {
                    l += b[n] * Math.Pow(256, n);
                    l = l - Math.Pow(2, b.Length * 8);
                }
                else
                {
                    l += b[n] * Math.Pow(256, n);
                }

            return l / 100;
        }

        public static double RawToQuantity(byte[] b)
        {
            double l = 0;
            for (var n = 0; n < b.Length; n++)
                if (n == b.Length - 1 && Accessory.GetBit(b[n], 7))
                {
                    l += b[n] * Math.Pow(256, n);
                    l = l - Math.Pow(2, b.Length * 8);
                }
                else
                {
                    l += b[n] * Math.Pow(256, n);
                }

            return l / 1000;
        }

        public static double RawToError(byte[] b)
        {
            double l = 0;
            for (var n = 0; n < b.Length; n++) l += b[n] * Math.Pow(256, n);
            return l;
        }

        public static string RawToData(byte[] b)
        {
            if (Accessory.PrintableByteArray(b))
                return "\"" + Encoding.GetEncoding(Settings.Default.CodePage).GetString(b) + "\"";
            return "[" + Accessory.ConvertByteArrayToHex(b) + "]";
        }

        public static double RawToBitfield(byte b)
        {
            return b;
        }
    }
}