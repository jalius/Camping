using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CampsiteSearch
{
    class SiteLinks
    {
        ///<summary>Used to find campsite listings for specific camp. {0}=parkId, {1}=startIdx. </summary> 
        public static string searchCampsites = "https://www.recreation.gov/campsitePaging.do?contractCode=NRSO&parkId={0}&startIdx={1}";
        ///<summary>Used to get campsite info. {0}=siteId, {1}=mm, {2}=dd, {3}=yyyy, {4}=parkId. arvDate format: mm%2Fdd%2Fyyyy</summary>
        public static string campsiteInfo = "https://www.recreation.gov/campsiteDetails.do?contractCode=NRSO&siteId={0}&arvdate={1}%2F{2}%2F{3}&parkId={4}";
        ///<summary>Used to reserve campsite. {0}=parkId, {1}=siteId, {2}=mm, {3}=dd, {4}=yyyy, {5}=lengthOfStay. arvDate format: mm%2Fdd%2Fyyyy</summary>
        public static string bookCampsite = "https://www.recreation.gov/switchBookingAction.do?contractCode=NRSO&parkId={0}&siteId={1}&arvdate={2}%2F{3}%2F{4}&lengthOfStay={5}";
        ///<summary>Used to login. {0}=email, {1}=password</summary>
        public static string login = "https://www.recreation.gov/memberSignInSignUp.do?AemailGroup_1733152645={0}&ApasswrdGroup_704558654={1}&sbmtCtrl=combinedFlowSignInKit&submitForm=submitForm";
    }
}
