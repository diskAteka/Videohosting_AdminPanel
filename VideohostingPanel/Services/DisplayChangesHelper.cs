using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideohostingPanel.Services
{
    public static class DisplayChangesHelper
    {
        public static string GetAffectedTablesString(List<string?>? affectedTables)
        {
            string output = string.Empty; 
            if (affectedTables == null)
                return output;
            
            for (int i = 0; i <= affectedTables.Count; i++)
                output += affectedTables[i] + "\n";
            return output;
        }
    }
}
