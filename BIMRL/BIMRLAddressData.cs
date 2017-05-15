//
// BIMRL (BIM Rule Language) Simplified Schema ETL (Extract, Transform, Load) library: this library transforms IFC data into BIMRL Simplified Schema for RDBMS. 
// This work is part of the original author's Ph.D. thesis work on the automated rule checking in Georgia Institute of Technology
// Copyright (C) 2013 Wawan Solihin (borobudurws@hotmail.com)
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3 of the License, or any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; If not, see <http://www.gnu.org/licenses/>.
//


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BIMRL;
using BIMRL.OctreeLib;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MeasureResource;
using Xbim.Ifc4.ExternalReferenceResource;
using Newtonsoft.Json;

namespace BIMRL
{
   class BIMRLAddressData
   {
      IIfcAddress theAddress;
      public BIMRLAddressData(IIfcAddress address)
      {
         theAddress = address;
      }

      public override string ToString()
      {
         if (theAddress is IIfcPostalAddress)
            return FormatPostalAddress(theAddress as IIfcPostalAddress);
         if (theAddress is IIfcTelecomAddress)
            return FormatTelecomAddress(theAddress as IIfcTelecomAddress);

         return base.ToString();
      }

      public string ToJsonString()
      {
         if (theAddress is IIfcPostalAddress)
            return PostalAddressJson(theAddress as IIfcPostalAddress);
         if (theAddress is IIfcTelecomAddress)
            return TelecomAddressJson(theAddress as IIfcTelecomAddress);

         return base.ToString();
      }

      string FormatPostalAddress(IIfcPostalAddress postalAddress)
      {
         string formattedPostalAddress = "";

         if (postalAddress.InternalLocation.HasValue)
            BIMRLCommon.appendToString(postalAddress.InternalLocation.Value.ToString(), " ,", ref formattedPostalAddress);

         foreach (IfcLabel addrLine in postalAddress.AddressLines)
            BIMRLCommon.appendToString(addrLine.ToString(), " ,", ref formattedPostalAddress);

         if (postalAddress.PostalBox.HasValue)
            BIMRLCommon.appendToString("PO Box: " + postalAddress.PostalBox.Value.ToString(), " ,", ref formattedPostalAddress);

         if (postalAddress.Town.HasValue)
            BIMRLCommon.appendToString(postalAddress.Town.Value.ToString(), " ,", ref formattedPostalAddress);

         if (postalAddress.Region.HasValue)
            BIMRLCommon.appendToString(postalAddress.Region.Value.ToString(), " ,", ref formattedPostalAddress);

         if (postalAddress.PostalCode.HasValue)
            BIMRLCommon.appendToString(postalAddress.PostalCode.Value.ToString(), " - ", ref formattedPostalAddress);

         if (postalAddress.Country.HasValue)
            BIMRLCommon.appendToString(postalAddress.Country.Value.ToString(), " ,", ref formattedPostalAddress);

         return formattedPostalAddress;
      }

      string FormatTelecomAddress(IIfcTelecomAddress telecomAddress)
      {
         string formattedTelecomAddress = "";
         if (telecomAddress.TelephoneNumbers.Count > 0)
         {
            BIMRLCommon.appendToString("Tel: ", null, ref formattedTelecomAddress);
            foreach (IfcLabel telNo in telecomAddress.TelephoneNumbers)
               BIMRLCommon.appendToString(telNo.ToString(), ", ", ref formattedTelecomAddress);
         }

         if (telecomAddress.FacsimileNumbers.Count > 0)
         {
            BIMRLCommon.appendToString("Fax: ", null, ref formattedTelecomAddress);
            foreach (IfcLabel faxNo in telecomAddress.FacsimileNumbers)
               BIMRLCommon.appendToString(faxNo.ToString(), ", ", ref formattedTelecomAddress);
         }

         if (telecomAddress.PagerNumber.HasValue)
            BIMRLCommon.appendToString("Pager: " + telecomAddress.PagerNumber.Value.ToString(), ", ", ref formattedTelecomAddress);

         if (telecomAddress.ElectronicMailAddresses.Count > 0)
         {
            BIMRLCommon.appendToString("e-mail: ", null, ref formattedTelecomAddress);
            foreach (IfcLabel email in telecomAddress.ElectronicMailAddresses)
               BIMRLCommon.appendToString(email.ToString(), ", ", ref formattedTelecomAddress);
         }

         if (telecomAddress.MessagingIDs.Count > 0)
         {
            BIMRLCommon.appendToString("Messaging ID: ", null, ref formattedTelecomAddress);
            foreach (IfcURIReference mesgID in telecomAddress.MessagingIDs)
               BIMRLCommon.appendToString(mesgID.ToString(), ", ", ref formattedTelecomAddress);
         }

         if (telecomAddress.WWWHomePageURL.HasValue)
            BIMRLCommon.appendToString("Website: " + telecomAddress.WWWHomePageURL.Value.ToString(), ", ", ref formattedTelecomAddress);

         return formattedTelecomAddress;
      }

      string PostalAddressJson(IIfcPostalAddress postalAddress)
      {
         return JsonConvert.SerializeObject(postalAddress);
      }

      string TelecomAddressJson(IIfcTelecomAddress telecomAddress)
      {
         return JsonConvert.SerializeObject(telecomAddress);
      }
   }
}
