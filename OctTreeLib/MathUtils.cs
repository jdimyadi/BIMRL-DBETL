using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BIMRL.OctreeLib
{
    public static class MathUtils
    {
        /*
        static double _doubleTol = Double.Epsilon;
        static float _floatTol = float.Epsilon;
        */

        // Precision up to 0.000001 should be sufficient for use in BIM
        static double _default_doubleTol = 1e-6;
        //static float _default_floatTol = 1e-6F; 
        static double _doubleTol = 1e-6;
        static float _floatTol = 1e-6F;
        static public int _doubleDecimalPrecision = 6; // from the above
        static public int _floatDecimalPrecision = 6; // from the above
        
        static decimal _decimalTol = 0;
        static int _intTol = 0;
        static short _shortTol = 0;
        static Int64 _int64Tol = 0;

        public static double defaultTol
        {
            get { return _default_doubleTol; }
        }
        
        public static double tol
        {
            get { return _doubleTol; }
            set { _doubleTol = (double) value; }
        }

        public static bool equalTol(double d1, double d2)
        {
            return (Math.Abs(d2 - d1) <= _doubleTol);
        }

        public static bool equalTol(double d1, double d2, double altTol)
        {
            return (Math.Abs(d2 - d1) <= altTol);
        }

        public static bool equalTol(float f1, float f2)
        {
            return (Math.Abs(f2 - f1) <= _floatTol);
        }
        public static bool equalTol(float f1, float f2, float altTol)
        {
            return (Math.Abs(f2 - f1) <= altTol);
        }

        public static bool equalTol(decimal dec1, decimal dec2)
        {
            return (Math.Abs(dec2 - dec1) <= _decimalTol);
        }
        public static bool equalTol(decimal dec1, decimal dec2, decimal altTol)
        {
            return (Math.Abs(dec2 - dec1) <= altTol);
        }

        public static bool equalTol(int i1, int i2)
        {
            return (Math.Abs(i2 - i1) <= _intTol);
        }
        public static bool equalTol(int i1, int i2, int altTol)
        {
            return (Math.Abs(i2 - i1) <= altTol);
        }

        public static bool equalTol(short s1, short s2)
        {
            return (Math.Abs(s2 - s1) <= _shortTol);
        }
        public static bool equalTol(short s1, short s2, short altTol)
        {
            return (Math.Abs(s2 - s1) <= altTol);
        }

        public static bool equalTol(Int64 i1, Int64 i2)
        {
            return (Math.Abs(i2 - i1) <= _int64Tol);
        }
        public static bool equalTol(Int64 i1, Int64 i2, Int64 altTol)
        {
            return (Math.Abs(i2 - i1) <= altTol);
        }

        public static bool equalTolSign(double d1, double d2)
        {
            double delta = Math.Abs(d2 - d1);
            return ((delta < _doubleTol) && (((d1 <= 0) && (d2 <= 0)) || ((d1 >= 0) && (d2 >= 0))));
        }

        public static bool equalTolSign(double d1, double d2, double altTol)
        {
            double delta = Math.Abs(d2 - d1);
            return ((delta < altTol) && (((d1 <= 0) && (d2 <= 0)) || ((d1 >= 0) && (d2 >= 0))));
        }
    }
}
