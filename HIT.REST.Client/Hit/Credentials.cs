using System;



namespace HIT.REST.Client.Hit {

  /// <summary>
  /// Anmeldeinformationen
  /// </summary>
  public class Credentials  {
//--------------------------------------------------------------------

    public AuthMode   AuthenticationMode  { get; set; }
    public bool       UseSecret           { get; set; }

    public string     Betriebsnummer      { get; set; }
    public string     Mitbenutzer         { get; set; }
    public string     PIN                 { get; set; }
    public int        TimeoutInSeconds    { get; set; }


    public int GetRequestTimeout() {
      return (UseSecret && TimeoutInSeconds > 0) ? TimeoutInSeconds : -Math.Abs(TimeoutInSeconds);
    }



//--------------------------------------------------------------------


    public Credentials()  {
      AuthenticationMode  = AuthMode.AuthenticationHeader;
    }

    public String getUser() {
      String strRet = Betriebsnummer;
      if (String.IsNullOrWhiteSpace(Mitbenutzer)) {
        strRet += "/"+Mitbenutzer;
      }
      return strRet;
    }
  }



//--------------------------------------------------------------------
}
