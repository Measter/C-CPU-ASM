using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using NCalc;

namespace C_CPU_Assembler
{
    public class Assembler
    {
        private uint m_currentAddress = 0;

        private readonly List<DirectoryInfo> m_includePaths = new List<DirectoryInfo>();

        private readonly Dictionary<string, string> m_variableTableFirstPass = new Dictionary<string, string>();
        private readonly Dictionary<string, UInt16> m_variableTable = new Dictionary<string, UInt16>();
        private readonly Dictionary<string, uint> m_lableTable = new Dictionary<string, uint>();

        private readonly Dictionary<string, OpCode> m_opcodeTable = new Dictionary<string, OpCode>();
        private readonly Dictionary<string, byte> m_registerTable = new Dictionary<string, byte>();

        private readonly List<FileEntry> m_errorEntries = new List<FileEntry>();
        private readonly List<FileEntry> m_warningEntries = new List<FileEntry>();
        private readonly List<FileEntry> m_entries = new List<FileEntry>();


        public int AssemblyLength => m_entries.Count;
        public int WarningCount => m_warningEntries.Count;
        public int ErrorCount => m_errorEntries.Count;

        public IEnumerable<string> ErrorList()
        {
            List<string> errMessages = new List<string>();

            foreach( FileEntry entry in m_errorEntries )
                errMessages.Add( $"File: {entry.FileName} - Line: {entry.LineNumber} - {FileEntry.GetFriendlyErrorCode( entry.ErrorCode )}" );

            return errMessages;
        }

        public IEnumerable<string> WarningList()
        {
            List<string> errMessages = new List<string>();

            foreach( FileEntry entry in m_warningEntries )
                errMessages.Add( $"File: {entry.FileName} - Line: {entry.LineNumber} - {FileEntry.GetFriendlyWarningCode( entry.WarningCode )}" );

            return errMessages;
        }


        public Assembler()
        {
            LoadTables();
        }

        private void LoadTables()
        {
            string[] lines;

            using( StreamReader sr = new StreamReader( Assembly.GetExecutingAssembly().GetManifestResourceStream( "C_CPU_Assembler.ISA.txt" ) ) )
                lines = sr.ReadToEnd().Split( '\n' );

            foreach( string l in lines )
            {
                if( String.IsNullOrWhiteSpace( l ) || l[0] == '#' )
                    continue;

                string[] parts = l.Trim().Split( new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries );

                if( parts[0] == "o" )
                {
                    OpCode opCode = new OpCode();
                    opCode.Value = UInt16.Parse( parts[1], NumberStyles.HexNumber );
                    opCode.Match = parts[2];

                    opCode.OperandA = parts[3] == "%r" ? OperandType.Register : OperandType.Literal;

                    if( parts.Length == 5 ) // Takes 2 operands.
                        opCode.OperandB = parts[4] == "%r" ? OperandType.Register : OperandType.Literal;

                    m_opcodeTable[opCode.Match] = opCode;
                } else if( parts[0] == "r" )
                {
                    m_registerTable[parts[2]] = byte.Parse( parts[1] );
                }
            }
        }


        public void AddIncludePath( string path )
        {
            DirectoryInfo dir = new DirectoryInfo( path );
            if( dir.Exists )
                m_includePaths.Add( dir );
        }




        public void Assemble( string codeFile )
        {
            string[] lines = File.ReadAllLines( codeFile );

            for( int i = 0; i < lines.Length; i++ )
            {
                string line = lines[i].Trim();

                FileEntry entry = new FileEntry( line, codeFile, i + 1, m_currentAddress );
                string[] parts;

                if( String.IsNullOrWhiteSpace( line ) || line[0] == ';' )
                {
                    // Save raw line for listing.
                    m_entries.Add( entry );
                    continue;
                }

                // Trim off comments for proper processing.
                line = line.Split( ';' )[0].Trim();

                if( line[0] == '#' ) // Line is a directive.
                {
                    entry.CodeType = CodeType.Directive;
                    parts = line.Split( new[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries );
                    if( parts[0] == "#equ" )
                        m_variableTableFirstPass[parts[1]] = parts[2];
                    else if (parts[0] == "#at")
                    {
                        m_currentAddress = parts[1][0] == 'h' ? UInt32.Parse(parts[1].Substring(1), NumberStyles.HexNumber) : UInt32.Parse(parts[1]);
                        entry.Address = m_currentAddress;
                    }
                } else if( line[0] == ':' ) // Line is label.
                {
                    entry.CodeType = CodeType.Label;
                    parts = line.Split( new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries );
                    m_lableTable[parts[0].Substring( 1 )] = m_currentAddress;
                } else
                {
                    OpCodeMatch match = MatchOpCode( line );
                    if( match == null )
                    {
                        entry.ErrorCode = ErrorCode.InvalidOpcode;
                        m_errorEntries.Add( entry );
                        continue;
                    }

                    entry.OpCode = match;
                    entry.Output = match.OpCode.Value;
                    entry.CodeType = CodeType.Instruction;

                    var opA = MatchOperand( match.OperandA );
                    OperandMatch opB = null;

                    if( !String.IsNullOrWhiteSpace( match.OperandB ) )
                        opB = MatchOperand( match.OperandB );

                    entry.OperandA = opA;
                    entry.OperandB = opB;

                    if( OperandErrorCheck( entry ) )
                    {
                        entry.ErrorCode = ErrorCode.InvalidOperands;
                        m_errorEntries.Add( entry );
                        continue;
                    }

                    if( ( match.OpCode.Value == 0x8800 || match.OpCode.Value == 0xB000 || match.OpCode.Value == 0x5000 || match.OpCode.Value == 0x5800 )
                            && opA.Register == opB.Register )
                    {
                        entry.WarningCode = WarningCode.RedundantStatement;
                        m_warningEntries.Add( entry );
                    }

                    m_currentAddress++;

                    if( opA?.IsRegister != true ) // First 7 instructions are the jump instructions.
                        m_currentAddress++;
                    if( opB != null && opB.IsRegister != true && match.OpCode.Value != 0x9000 ) // SETI instruction doesn't need second word.
                        m_currentAddress++;
                }

                m_entries.Add( entry );
            }
        }


        // Return true on error.
        private bool OperandErrorCheck( FileEntry entry )
        {
            bool res = false;

            switch( entry.OpCode.OpCode.OperandA )
            {
                case OperandType.Register:
                    if( !entry.OperandA.IsRegister )
                        res = true;
                    break;
                case OperandType.Literal:
                    if( entry.OperandA.IsRegister )
                        res = true;
                    break;
            }

            switch( entry.OpCode.OpCode.OperandB )
            {
                case OperandType.None:
                    if( entry.OperandB != null )
                        res = true;
                    break;
                case OperandType.Register:
                    if( !entry.OperandB.IsRegister )
                        res = true;
                    break;
                case OperandType.Literal:
                    if( entry.OperandB.IsRegister )
                        res = true;
                    break;
            }

            return res;
        }

        public bool AssembleSecondStage()
        {
            EvaluateVariables();

            foreach( FileEntry entry in m_entries )
            {
                if( entry.OpCode != null && entry.ErrorCode == ErrorCode.None )
                {
                    var operandA = entry.OperandA;
                    var operandB = entry.OperandB;

                    if (operandA.IsRegister)
                        entry.Output |= (UInt16) (operandA.Register << 8);
                    else
                        entry.Literal = ParseExpression(operandA.Literal); // Only OpCodes with this state are jumps.


                    if( operandB != null )
                    {
                        if( operandB.IsRegister )
                            entry.Output |= operandB.Register;
                        else
                        {
                            UInt16 literal = ParseExpression( operandB.Literal );

                            // SETI can only take a 8-bit literal.
                            if( entry.OpCode.OpCode.Value == 0x9000 )
                            {
                                if( literal > 0xFF )
                                {
                                    entry.ErrorCode = ErrorCode.InvalidOperands;
                                    m_errorEntries.Add( entry );
                                    return false;
                                }

                                entry.Output |= literal;
                            } else
                            {
                                entry.Literal = literal;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private void EvaluateVariables()
        {
            bool finishedParsing = false;

            while( !finishedParsing )
            {
                finishedParsing = true;

                foreach( string variables in m_variableTableFirstPass.Keys )
                {
                    Expression e = new Expression( m_variableTableFirstPass[variables] );
                    e.EvaluateParameter += ( name, args ) =>
                    {
                        if( m_lableTable.ContainsKey( name ) )
                            args.Result = m_lableTable[name];
                        if( m_variableTable.ContainsKey( name ) )
                            args.Result = m_variableTable[name];
                        else if( m_variableTableFirstPass.ContainsKey( name ) )
                            args.Result = m_variableTableFirstPass[name];
                        else if( Char.ToLower( name[0] ) == 'h' ) // Assume this is a hex number.
                        {
                            int bRes;
                            if( Int32.TryParse( name.Substring( 1 ), NumberStyles.HexNumber, null, out bRes ) )
                                args.Result = bRes;
                            else
                                args.Result = -1;
                        } else
                            args.Result = -1;
                    };

                    var res = e.Evaluate();

                    if( res is int )
                        m_variableTable[variables] = (UInt16)(int)res;
                    else if( res is double )
                        m_variableTable[variables] = (UInt16)(double)res;
                    else
                    {
                        m_variableTableFirstPass[variables] = (string)res;
                        finishedParsing = false;
                    }
                }
            }
        }

        private UInt16 ParseExpression( string literal )
        {
            Expression exp = new Expression( literal );
            exp.EvaluateParameter += ( name, args ) =>
            {
                if( m_lableTable.ContainsKey( name ) )
                    args.Result = m_lableTable[name];
                else if( m_variableTable.ContainsKey( name ) )
                    args.Result = m_variableTable[name];
                else if( Char.ToLower( name[0] ) == 'h' ) // Assume this is a hex number.
                {
                    int bRes;
                    if( Int32.TryParse( name.Substring( 1 ), NumberStyles.HexNumber, null, out bRes ) )
                        args.Result = bRes;
                    else
                        args.Result = -1;
                } else
                    args.Result = -1;
            };

            var res = exp.Evaluate();

            if( res is int )
                return (UInt16)(int)res;

            if( res is uint )
                return (UInt16)(uint)res;

            if( res is byte )
                return (UInt16)(byte)res;

            if( res is double )
                return (UInt16)(double)res;

            return 0xffff;
        }


        public void OutputBinary( string outputFile )
        {
            using( FileStream fs = new FileStream( outputFile, FileMode.Create, FileAccess.Write, FileShare.None ) )
            using( BinaryWriter bw = new BinaryWriter( fs ) )
            {
                foreach( FileEntry entry in m_entries )
                {
                    if( entry.CodeType != CodeType.Instruction || entry.ErrorCode != ErrorCode.None )
                        continue;

                    bw.Write( (byte)( entry.Output >> 8 ) );
                    bw.Write( (byte)( entry.Output & 0xFF ) );
                    if( entry.Literal.HasValue )
                    {
                        bw.Write( (byte)( entry.Literal.Value >> 8 ) );
                        bw.Write( (byte)( entry.Literal.Value & 0xFF ) );
                    }
                }
            }
        }

        public void OutputListing( string listingFile )
        {
            StringBuilder listing = new StringBuilder();

            listing.AppendLine( "C-CPU Assembler Listing" );
            listing.AppendLine( $"Created on {DateTime.Now}" );
            listing.AppendLine();

            foreach( FileEntry entry in m_entries )
            {
                listing.Append( $"{entry.Address:X4}  " );

                if( entry.CodeType == CodeType.Instruction )
                {
                    listing.Append( $"{entry.Output:X4} " );
                    listing.Append( entry.Literal.HasValue ? $"{entry.Literal.Value:X4}  " : "      " );
                } else
                {
                    listing.Append( "".PadLeft( 11 ) );
                }

                listing.Append( $"{entry.LineNumber}  " );
                listing.AppendLine( entry.Line );
            }

            File.WriteAllText( listingFile, listing.ToString() );
        }



        private OpCodeMatch MatchOpCode( string value )
        {
            string[] parts = value.Trim().Split( new[] { ' ', '\t' }, 2 );
            string[] operands = parts[1].Split( ',' );
            OpCodeMatch match = new OpCodeMatch();
            match.Original = value;

            if( m_opcodeTable.ContainsKey( parts[0].ToLower() ) )
            {
                match.OpCode = m_opcodeTable[parts[0]];
                match.OperandA = operands[0].Trim();

                if( operands.Length > 1 )
                    match.OperandB = operands[1].Trim();

                return match;
            }

            return null;
        }

        private OperandMatch MatchOperand( string value )
        {
            value = value.Trim();
            OperandMatch match = new OperandMatch();
            match.Original = value;

            if( m_registerTable.ContainsKey( value ) )
            {
                match.IsRegister = true;
                match.Register = m_registerTable[value];
            } else
            {
                match.IsRegister = false;
                match.Literal = value;
            }

            return match;
        }
    }

    public enum OperandType
    {
        None, Register, Literal
    }

    public class OperandMatch
    {
        public bool IsRegister
        {
            get; set;
        }
        public byte Register
        {
            get; set;
        }
        public string Literal
        {
            get; set;
        }

        public string Original
        {
            get; set;
        }
    }

    [DebuggerDisplay( "{Match} - {OperandA} - {OperandB}" )]
    public class OpCode
    {
        public OperandType OperandA
        {
            get; set;
        }
        public OperandType OperandB
        {
            get; set;
        }
        public string Match
        {
            get; set;
        }
        public UInt16 Value
        {
            get; set;
        }
    }

    [DebuggerDisplay( "{OpCode.Match}" )]
    public class OpCodeMatch
    {
        public OpCode OpCode
        {
            get; set;
        }
        public string OperandA
        {
            get; set;
        }
        public string OperandB
        {
            get; set;
        }
        public string Original
        {
            get; set;
        }
    }
}