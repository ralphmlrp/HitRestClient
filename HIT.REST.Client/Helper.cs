using System;



namespace HIT.REST.Client {

  class Helper {
//--------------------------------------------------------------------

    public static String getForNum(int num,String singular,String plural,String token = null) {
      String ret = (num == 1) ? singular : plural;
      if (!String.IsNullOrEmpty(token)) {
        ret = ret.Replace(token,num.ToString());
      }
      return ret;
    }



//--------------------------------------------------------------------
    // HEX ENCODING

    /// <summary>
    /// Zulässige Hex-String-Zeichen in richtiger Reihenfolge für das 16er-System
    /// </summary>
    private static readonly char[] HEX_DIGITS = "0123456789ABCDEF".ToCharArray();



    /// <summary>
    /// Encodiere ganzes byte[] in Hex-String.
    /// </summary>
    /// <param name="pabyteData">byte[]</param>
    /// <returns>Hex-String</returns>
    public static String hexEncode(byte[] pabyteData) {
      if (pabyteData == null) {
        return null;
      }
      return hexEncode(pabyteData,0,pabyteData.Length);
    }



    /// <summary>
    /// Encodiere Teil eines byte[] in Hex-String.
    /// </summary>
    /// <param name="pabyteData">byte[]</param>
    /// <param name="pintOffset">0-basierter Start-Offset im Array</param>
    /// <param name="pintLength">Länge ab Start-Offset im Array</param>
    /// <returns>Hex-String</returns>
    public static String hexEncode(byte[] pabyteData,int pintOffset,int pintLength) {
      if (pabyteData == null)  throw new ArgumentNullException();

      char[] acharBuf = new char[pintLength * 2];
      for (int intByteIndex = 0, intHexIndex = 0, intChar; intByteIndex < pintLength;) {
        intChar = pabyteData[pintOffset + intByteIndex++];
        acharBuf[intHexIndex++] = HEX_DIGITS[((uint)intChar >> 4) & 0x0F];
        acharBuf[intHexIndex++] = HEX_DIGITS[intChar & 0x0F];
      }
      return new String(acharBuf);
    }



    /// <summary>
    /// Decodiere Hex-String in byte[].
    /// </summary>
    /// <param name="pstrHexData">Hex-String</param>
    /// <returns>byte[]</returns>
    public static byte[] hexDecode(String pstrHexData) {
      if (pstrHexData == null)  throw new ArgumentNullException();

      int intLen = pstrHexData.Length;
      byte[] abyteResult = new byte[((intLen + 1) / 2)];
      int intByteIndex = 0, intHexIndex = 0;
      if ((intLen % 2) == 1) {
        abyteResult[intHexIndex++] =  (byte)hexDigitToChar(pstrHexData[intByteIndex++]);
      }
      while (intByteIndex < intLen) {
        abyteResult[intHexIndex] =  (byte)(hexDigitToChar(pstrHexData[intByteIndex++]) << 4);
        abyteResult[intHexIndex++] |= (byte)hexDigitToChar(pstrHexData[intByteIndex++]);
      }
      return abyteResult;
    }



    private static int hexDigitToChar(char charHex) {
      if (charHex >= '0' && charHex <= '9') {
        return charHex - '0';
      }
      else if (charHex >= 'A' && charHex <= 'F') {
        return charHex - 'A' + 10;
      }
      else if (charHex >= 'a' && charHex <= 'f') {
        return charHex - 'a' + 10;
      }
      else {
        throw new ArgumentException("Invalid hexadecimal digit: "+charHex);
      }
    }



//--------------------------------------------------------------------
  }
}
