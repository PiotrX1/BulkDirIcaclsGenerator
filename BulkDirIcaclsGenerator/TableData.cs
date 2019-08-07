using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkDirIcaclsGenerator
{
    class TableData
    {
        private DirectoryInfo DirectoryInfo;
        public string Name { get; }
        public string FullName { get; }
        public List<string> Permissions { get; set; }

        public TableData(DirectoryInfo info)
        {
            this.DirectoryInfo = info;
            this.Name = info.Name;
            this.FullName = info.FullName;
            this.Permissions = new List<string>();
        }
        public void addColumn()
        {
            this.Permissions.Add(string.Empty);
        }
    }
}
