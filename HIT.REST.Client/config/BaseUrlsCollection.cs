using System;
using System.Configuration;



namespace HIT.REST.Client.config {

  /// <summary>
  /// Beschreibt Element &lt;BaseUrls&gt; als Collection.
  /// </summary>
  [ConfigurationCollection(typeof(BaseUrlElement))]
  public class BaseUrlsCollection : ConfigurationElementCollection  {
//--------------------------------------------------------------------


    /// <summary>
    /// Legt neues Element für die Collection an, welches anschließend
    /// über dessen ConfigurationProperty mit Werten gefüllt wird
    /// </summary>
    /// <returns></returns>
    protected override ConfigurationElement CreateNewElement() {
      return new BaseUrlElement();
    }

    /// <summary>
    /// Da das ConfigurationElement in einer Map gespeichert wird,
    /// muss die ConfigurationElementCollection wissen, unter welchem Namen.
    /// Der wird mit diesem hier geliefert: hier ist es die Kombination
    /// aller Properties als URL.
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    protected override object GetElementKey(ConfigurationElement element) {
      return ((BaseUrlElement)element).BaseUrl;
    }


    /// <summary>
    /// Hinzufügen einer BaseUrl.
    /// </summary>
    /// <param name="pobjElement"><see cref="BaseUrlElement"/></param>
    /// <returns>eigene Instanz zur Methodenverkettung</returns>
    public void Add(BaseUrlElement pobjElement) {
      if (pobjElement == null)  throw new ArgumentNullException();

      this.BaseAdd(pobjElement);
    }
    public void Add(Object pobjNew) {
      if (pobjNew == null)  throw new ArgumentNullException();

      if (pobjNew is BaseUrlElement)  {
         Add((BaseUrlElement)pobjNew);
      }
      else  {
        throw new ArgumentException("Invalid type "+pobjNew.GetType()+" for "+GetType());
      }
    }



    /// <summary>
    /// Liefere <see cref="BaseUrlElement"/> an der gegebenen Position.
    /// </summary>
    /// <param name="index">0-basierter Index</param>
    /// <returns><see cref="BaseUrlElement"/> oder <see cref="IndexOutOfRangeException"/></returns>
    public BaseUrlElement this[int index] { get {
      return (BaseUrlElement)BaseGet(index);
    }}



//--------------------------------------------------------------------
  }

}
