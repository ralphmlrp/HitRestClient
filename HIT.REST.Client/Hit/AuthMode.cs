



namespace HIT.REST.Client.Hit {

  /// <summary>Art der Autorisierung</summary>
  public enum AuthMode  {
    /// <summary>Anfrage ohne Autorisierung</summary>
    NoAuth                = 0,
    /// <summary>Anfrage nur per QueryStrings</summary>
    QueryString           = 1,
    /// <summary>Anfrage mit "basic"-Autorisierungskopfzeile in der Form "bnr:mbn:pin" plus eigenen HTTP-Kopfzeilen für Timeout und Secret</summary>
    AuthenticationHeader  = 2,
    /// <summary>Anfrage nur mit eigenen HTTP-Kopfzeilen wie "hit-bnr" etc</summary>
    SelfmadeHeader        = 3,
  }



}
