using Pushthrough.Web.Services.Interface;
using System.Collections.Generic;

namespace Pushthrough.Web.Services
{
    public class PushServiceResult<TEntity> : IPushServiceResult<TEntity>
    {
        public long Key { get; private set; }
        public TEntity Entity { get; private set; }
        public string Device { get; set; }
        public bool IsCancelled { get; private set; }
        public PushServiceResult(long key, string device,  TEntity entity)
        {
            this.Key = key;
            this.Device = device;
            this.Entity = entity;
            IsCancelled = false;
        }

        public PushServiceResult<TEntity> Cancel()
        {
            IsCancelled = true;
            return this;
        }
    }
}