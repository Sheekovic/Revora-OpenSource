"""Provision only Revora DeityLand catalog rows; default is transaction rollback."""
from pathlib import Path
import sys
from deityland_spawns import spawn_cells
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'obj/production-owner-acceptance'))
from db_access import connect
ROOT=Path(__file__).resolve().parents[1]
cells,_=spawn_cells(ROOT)
apply='--apply' in sys.argv
with connect() as c:
 try:
  with c.cursor() as q:
   q.execute('SELECT id FROM maps WHERE id=%s',(10250,));assert q.fetchone() is None,'Map already provisioned; inspect before rerunning'
   q.execute('SELECT id FROM monsterinfos WHERE id=%s',(990250,));assert q.fetchone() is None
   q.execute('SELECT id FROM npcs WHERE id IN (%s,%s)',(900000171,900000172));assert not q.fetchall()
   q.execute('SELECT id FROM monsterspawns WHERE id BETWEEN %s AND %s',(990250000,990250039));assert not q.fetchall()
   q.execute('INSERT INTO maps(id,mapdoc,type,weather) VALUES(%s,%s,%s,%s)',(10250,10250,8192,0))
   q.execute('INSERT INTO monsterinfos(id,name,type,lookface,life,mana,attack_max,attack_min,defence,attack_range,view_range,attack_speed,move_speed,level,attack_user,run_speed,magic_type,extra_exp,ownitem,ownitemrate) SELECT %s,name,type,lookface,%s,mana,%s,%s,defence,%s,view_range,attack_speed,move_speed,%s,attack_user,run_speed,magic_type,0,0,0 FROM monsterinfos WHERE id=%s',(990250,60460,1800,1200,3,120,3634));assert q.rowcount==1
   for i,(x,y) in enumerate(cells[:40]):
    q.execute('INSERT INTO monsterspawns(id,mapid,bound_x,bound_y,bound_cx,bound_cy,maxnpc,rest_secs,max_per_gen,npctype) VALUES(%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)',(990250000+i,10250,x,y,1,1,1,25,1,990250))
   for row in [(900000171,'DeityGuide',2,50340,1002,396,344,0),(900000172,'SaintTaiyi',2,50340,10250,1021,1281,0)]:
    q.execute('INSERT INTO npcs(id,name,type,lookface,mapid,cellx,celly,effect) VALUES(%s,%s,%s,%s,%s,%s,%s,%s)',row)
   q.execute('SELECT count(*) FROM monsterspawns WHERE mapid=%s AND npctype=%s',(10250,990250));assert q.fetchone()[0]==40
   if apply:c.commit();print('Provisioned DeityLand, two NPCs and 40 reachable spawns.')
   else:c.rollback();print('Provisioning checks passed; all changes rolled back.')
 except: c.rollback();raise
