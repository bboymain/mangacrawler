﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MangaCrawlerLib
{
    public static class Extensions
    {
        public static String RemoveFromRight(this string a_str, int a_chars)
        {
            return a_str.Remove(a_str.Length - a_chars);
        }

        public static String RemoveFromLeft(this string a_str, int a_chars)
        {
            return a_str.Remove(0, a_chars);
        }

        public static String NoAsterix(this string a_str)
        {
            if (a_str.Last() == '*')
                return a_str.RemoveFromRight(1);
            else
                return a_str;
        }
    }
}
