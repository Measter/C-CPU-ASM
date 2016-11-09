using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Configuration;
using System.Text;
using System.Threading.Tasks;

namespace C_CPU_Assembler
{
    class Program
    {
        private static string m_codeFile = null, m_outputFile = null, m_listingFile = null;
        private static bool m_outputListing = false;
        private static Assembler m_asm;

        public static int Main( string[] args )
        {
            Console.WriteLine( "C-CPU Assembler" );

            if( args.Length == 0 )
            {
                OutputHelp();
                return 1;
            }

            m_asm = new Assembler();

            m_asm.AddIncludePath( Environment.CurrentDirectory );

            bool fatalExit = ParseArguments( args );
            if( fatalExit )
                return 1;

            if( String.IsNullOrWhiteSpace( m_codeFile ) )
            {
                Console.WriteLine( "Error: No code file specified.\nUse ccpuasm -h for usage." );
                return 1;
            }

            if( !File.Exists( m_codeFile ) )
            {
                Console.WriteLine( $"Error: Code file not found: {m_codeFile}" );
                return 1;
            }

            if( String.IsNullOrWhiteSpace( m_outputFile ) )
                m_outputFile = Path.ChangeExtension( m_codeFile, ".bin" );

            if( m_outputListing && String.IsNullOrWhiteSpace( m_listingFile ) )
                m_listingFile = Path.ChangeExtension( m_codeFile, ".lst" );

            Console.Write( "Assembling..." );
            m_asm.Assemble( m_codeFile );

            if( m_asm.AssemblyLength == 0 )
            {
                Console.WriteLine( "Nothing to assemble." );
                return 0;
            }

            if( m_asm.ErrorCount > 0 )
            {
                Console.WriteLine( "Errors in assembly: " );
                foreach( string err in m_asm.ErrorList() )
                    Console.WriteLine( err );

                return 1;
            }

            if( m_asm.WarningCount > 0 )
            {
                Console.WriteLine( "Warnings: " );
                foreach( string warn in m_asm.WarningList() )
                    Console.WriteLine( warn );
            }

            Console.WriteLine( "Stage 2..." );
            m_asm.AssembleSecondStage();

            Console.WriteLine($"Writing Binary to {m_outputFile}...");
            m_asm.OutputBinary( m_outputFile );

            if (m_outputListing && !String.IsNullOrWhiteSpace(m_listingFile))
            {
                Console.WriteLine($"Writing listing to {m_listingFile}...");
                m_asm.OutputListing(m_listingFile);
            }

            return 0;
        }

        private static bool ParseArguments( string[] args )
        {
            for( int i = 0; i < args.Length; i++ )
            {
                // If argument doesn't start with a -, then it should be an input or output file.
                if( args[i][0] != '-' )
                {
                    if( String.IsNullOrWhiteSpace( m_codeFile ) )
                        m_codeFile = args[i];
                    else if( String.IsNullOrWhiteSpace( m_outputFile ) )
                        m_outputFile = args[i];
                    else
                    {
                        Console.WriteLine( $"Error: Invalid argument: {args[i]}\n Use ccpuasm -h for usage." );
                        return true;
                    }

                    continue;
                }

                // Argument is an option.
                switch( args[i] )
                {
                    case "-i":
                        m_asm.AddIncludePath( args[++i] );
                        break;
                    case "-l":
                        m_outputListing = true;
                        break;
                    case "-lname":
                        m_listingFile = args[++i];
                        m_outputListing = true;
                        break;

                    default:
                        Console.WriteLine( $"Error: Unknown argument: {args[i]}.\nUse ccpuasm -h for usage." );
                        return true;
                }
            }

            return false;
        }

        private static void OutputHelp()
        {
            throw new NotImplementedException();
        }
    }
}
