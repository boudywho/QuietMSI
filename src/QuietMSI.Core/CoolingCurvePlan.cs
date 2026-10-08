using System.Globalization;
namespace QuietMSI.Core;

public static class CoolingCurvePlan
{
    public static bool SameStableFields(byte[] a,byte[] b) => a.Length==8 && b.Length==8 && a.AsSpan(1,7).SequenceEqual(b.AsSpan(1,7));
    public static byte[] Create(byte[] original, string csv)
    {
        if (original.Length != 8 || original[7] is <20 or >100) throw new ArgumentException("Original curve is not safely bounded.");
        var parts=csv.Split(','); if(parts.Length!=6) throw new ArgumentException("Exactly six percentages required.");
        var result=(byte[])original.Clone();
        for(var i=0;i<6;i++)
        {
            if(!int.TryParse(parts[i],NumberStyles.None,CultureInfo.InvariantCulture,out var v) || v<Math.Max(20,(int)original[i+1]) || v>original[7]
                || i>0 && v<result[i]) throw new ArgumentException("Curve must be monotonic, no lower than captured original, and within original terminal limit.");
            result[i+1]=(byte)v;
        }
        return result;
    }
}
