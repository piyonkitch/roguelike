using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;

using System.Runtime.Serialization;
using System.Xml;

namespace Maze
{
    [Serializable]
    class Item
    {
        public string name {
            get { return entity.name; }
            // set は、なし
        }
        public int num { get; set; }
        public Entity entity { get; set; } // Entity のサブクラスの use() を使うために、Entity を記憶しておく

        public Item(Entity e)
        {
            num = 1;
            entity = e;
        }

        public void add()
        {
            num++;
        }

        public bool drop(Entity user)
        {
            Debug.Assert(num > 0);
            if (user.weapon == this.entity)
            {
                Console.WriteLine("使用中の武器なので捨てられない");
                return false;
            }
            if (user.armor == this.entity)
            {
                Console.WriteLine("着用中の鎧なので捨てられない");
                return false;
            }
            num--;
            Console.WriteLine("{0} は {1} を捨てた", user, name);
            return true;
        }

        public virtual void use(Entity user)
        {
            Debug.Assert(num > 0);
            if (!entity.isUsable())
            {
                Console.WriteLine("これは、使うものではないな");
                return;
            }
            entity.use(user);
            Console.WriteLine("{0} を使った", name);
            num--;
        }

        public virtual void wield(Entity user)
        {
            Debug.Assert(num > 0);
            if (!entity.isWieldable())
            {
                Console.WriteLine("これは、武器ではないな");
                return;
            }
            entity.wield(user);
            Console.WriteLine("{0} を構えた", name);
        }

        public virtual void wear(Entity user)
        {
            Debug.Assert(num > 0);
            if (!entity.isWearable())
            {
                Console.WriteLine("これは、鎧ではないな");
                return;
            }
            entity.wear(user);
            Console.WriteLine("{0} を着用した", name);
        }
    }
}
