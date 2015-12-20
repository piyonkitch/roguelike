/*
Copyright(c) 2015, piyonkitch<kazuo.horikawa.ko@gmail.com>
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
 list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
 this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of roguelike nor the names of its
 contributors may be used to endorse or promote products derived from
  this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
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
