using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C_CPU_Assembler
{
    public static class Extensions
    {
        public static int SafeIndexOf( this string value, char needle )
        {
            return SafeIndexOf( value, needle, 0 );
        }

        public static int SafeIndexOf( this string value, char needle, int startIndex )
        {
            value = value.Trim();
            bool inString = false, inChar = false;
            for( int i = startIndex; i < value.Length; i++ )
            {
                if( value[i] == needle && !inString && !inChar )
                    return i;
                if( value[i] == '"' && !inChar )
                    inString = !inString;
                if( value[i] == '\'' && !inString )
                    inChar = !inChar;
            }
            return -1;
        }

        public static int SafeIndexOfParenthesis( this string value, char needle )
        {
            return SafeIndexOfParenthesis( value, needle, 0 );
        }

        public static int SafeIndexOfParenthesis( this string value, char needle, int startIndex )
        {
            value = value.Trim();
            int parenthesis = 0;
            for( int i = startIndex; i < value.Length; i++ )
            {
                if( value[i] == needle && parenthesis == 0 )
                    return i;
                if( value[i] == '(' )
                    parenthesis++;
                if( value[i] == ')' )
                    parenthesis--;
            }
            return -1;
        }
    }
}
