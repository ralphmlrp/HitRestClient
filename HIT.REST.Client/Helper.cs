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
  }
}
