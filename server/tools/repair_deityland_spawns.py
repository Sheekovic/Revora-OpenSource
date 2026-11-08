"""Correct only the 40 existing DeityLand spawns; dry-run unless --apply."""
from pathlib import Path
import json
import sys
from deityland_spawns import spawn_cells

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'obj/production-owner-acceptance'))
from db_access import connect

points,regions=spawn_cells(ROOT)
apply='--apply' in sys.argv
with connect() as c:
    try:
        with c.cursor() as q:
            q.execute('SELECT id,bound_x,bound_y,bound_cx,bound_cy,maxnpc,max_per_gen,rest_secs FROM monsterspawns WHERE mapid=%s AND npctype=%s ORDER BY id FOR UPDATE',(10250,990250))
            before=q.fetchall()
            assert [row[0] for row in before] == list(range(990250000,990250040))
            assert all(row[3:]==(1,1,1,1,25) for row in before),'Unexpected spawn settings; inspect before changing'
            if apply:
                backup=Path('C:/Revora/DeploymentBackups/deityland-spawns-20261005')
                backup.mkdir(parents=True,exist_ok=True)
                with (backup/'monsterspawns.json').open('x') as f:
                    json.dump(before,f,indent=2)
            for row,(x,y) in zip(before,points):
                q.execute('UPDATE monsterspawns SET bound_x=%s,bound_y=%s WHERE id=%s AND mapid=%s AND npctype=%s AND bound_x=%s AND bound_y=%s',(x,y,row[0],10250,990250,row[1],row[2]))
                assert q.rowcount==1
            q.execute('SELECT bound_x,bound_y FROM monsterspawns WHERE mapid=%s AND npctype=%s ORDER BY id',(10250,990250))
            assert q.fetchall()==points
            if apply:c.commit()
            else:c.rollback()
            print(('Applied' if apply else 'Validated and rolled back')+' 40 placements across 15 reachable hunting areas; rewards/stats/counts unchanged.')
    except:
        c.rollback()
        raise
