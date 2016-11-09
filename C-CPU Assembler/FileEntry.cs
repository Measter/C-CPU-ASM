using System;
using System.Diagnostics;

namespace C_CPU_Assembler
{
    [DebuggerDisplay( "{CodeType} - {Line}" )]
    public class FileEntry
    {
        public FileEntry( string line, string codeFile, int lineNumber, uint currentAddress )
        {
            Line = line;
            FileName = codeFile;
            LineNumber = lineNumber;
            Address = currentAddress;
            Literal = null;
        }

        public string Line
        {
            get; private set;
        }
        public string FileName
        {
            get; private set;
        }
        public int LineNumber
        {
            get; private set;
        }
        public UInt32 Address
        {
            get; set;
        }


        public OpCodeMatch OpCode
        {
            get; set;
        }
        public OperandMatch OperandA
        {
            get; set;
        }
        public OperandMatch OperandB
        {
            get; set;
        }

        public UInt16 Output
        {
            get; set;
        }

        public UInt16? Literal { get; set; }

        public ErrorCode ErrorCode
        {
            get; set;
        }
        public CodeType CodeType
        {
            get; set;
        }
        public WarningCode WarningCode
        {
            get; set;
        }


        public static string GetFriendlyErrorCode( ErrorCode code )
        {
            switch( code )
            {
                case ErrorCode.InvalidOpcode:
                    return "Invalid opcode.";
                case ErrorCode.InvalidOperands:
                    return "Invalid operands to opcode.";
                default:
                    return code + ".";
            }
        }

        public static string GetFriendlyWarningCode( WarningCode code )
        {
            switch( code )
            {
                case WarningCode.RedundantStatement:
                    return "Redundant statement.";
                default:
                    return code + ".";
            }
        }

    }
}