using System;
using System.Collections.Generic;
using System.Text;
using Tasks.Model;

namespace IOOperation.BaseUtilityServices
{
    public class OperationModel
    {
        public string Key { get; set; }
        public string TaskName { get; set; }
        public string Description { get; set; }
        public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    }
}
