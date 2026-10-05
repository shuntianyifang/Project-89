"""Reproducible first-pass 2 km abstraction of BKG TK200; assumptions stay explicit."""
import copy
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / 'Scripts/Data/Scenarios/Fulda_Gap'
REF = ROOT / 'docs/research/Fulda_Gap'

def write(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def line(points):
    cells = []
    for (ax, ay), (bx, by) in zip(points, points[1:]):
        n = max(abs(bx-ax), abs(by-ay))
        for i in range(n+1):
            t = i/max(n, 1)
            p = (round(ax+(bx-ax)*t), round(ay+(by-ay)*t))
            if p not in cells: cells.append(p)
    return cells

def main():
    # Raster colour sampling extracts forest COVER, not elevation or line of sight.
    image = Image.open(REF / 'tk200_candidate.png').convert('RGB')
    assert image.size == (2000, 1200)
    terrain = [[0]*50 for _ in range(30)]
    infra = [[0]*50 for _ in range(30)]
    for y in range(30):
        for x in range(50):
            crop = image.crop((x*40,y*40,(x+1)*40,(y+1)*40))
            pixels = list(crop.get_flattened_data() if hasattr(crop,'get_flattened_data') else crop.getdata())
            green = sum(g > r+8 and g > b+12 for r,g,b in pixels)/1600
            terrain[y][x] = 1 if green >= .28 else 0
    # Manual, coarse tracing against the georeferenced historical scan (S07).
    # Every coordinate is an abstraction, not survey accuracy or a bridge inventory.
    roads = [
        {'name':'A4 northern corridor','kind':2,'points':[[0,8],[12,8],[16,5],[21,4],[25,3],[31,2],[38,2],[49,3]]},
        {'name':'A7 rear north-south corridor','kind':2,'points':[[15,0],[14,5],[13,8],[13,13],[14,18],[14,23],[14,29]]},
        {'name':'B27 Fulda–Hünfeld–Bad Hersfeld','kind':1,'points':[[13,28],[14,22],[15,18],[14,12],[13,8],[16,2]]},
        {'name':'B84 Hünfeld–Rasdorf–Geisa direction','kind':1,'points':[[15,18],[19,16],[22,15],[24,14],[25,11]]},
        {'name':'Vacha–Heringen connection','kind':1,'points':[[25,10],[24,8],[23,7],[20,7],[18,8],[13,8]]},
        {'name':'Meiningen–Tann approach','kind':1,'points':[[38,24],[34,21],[30,19],[26,19],[24,20],[22,21]]}
    ]
    for road in roads:
        for x,y in line(road['points']): infra[y][x] = max(infra[y][x], road['kind'])
    places = [
        ('Fulda',13,28),('Bad Hersfeld',13,8),('Bebra',16,2),('Hünfeld',15,18),
        ('Rasdorf',21,15),('Geisa',24,16),('Vacha',25,10),('Heringen',23,7),
        ('Eisenach',34,1),('Bad Salzungen',35,11),('Tann',24,19),('Meiningen',39,24)
    ]
    for name,x,y in places:
        terrain[y][x] = 3 if name in ['Fulda','Bad Hersfeld','Eisenach','Meiningen'] else 2
    # Small rivers are represented as crossing edges, not impassable 2 km-wide water.
    rivers = [
        {'name':'Fulda','points':[[13,29],[12,25],[12,22],[10,19],[10,16],[12,12],[14,8],[17,3],[17,0]]},
        {'name':'Werra','points':[[38,29],[39,24],[37,20],[34,16],[33,13],[29,11],[26,10],[25,7],[26,4],[25,0]]}
    ]
    # Row-wise digitization of border shape; centre attribution at 2 km resolution.
    # Coarse bends including the Geisa salient; do not treat this as cadastral geometry.
    border = [26,26,26,26,25,25,25,24,24,24,24,23,22,21,20,20,21,22,23,24,25,25,26,26,27,27,28,28,29,29]
    control = [''.join('1' if x < border[y] else '2' for x in range(50)) for y in range(30)]
    blocked, bridges = [], []
    for river in rivers:
        for x,y in line(river['points']):
            if not 0 < x < 50: continue
            edge = [x-1,y,x,y]
            # At this abstraction bridges exist where a traced road crosses the river.
            if infra[y][x] or infra[y][x-1]: bridges.append(edge)
            else: blocked.append(edge)
    map_data = {
        'width':50,'height':30,'cell_km':2,'hours_per_round':2,'crs':'EPSG:25832',
        'bounds_m':[520000,5590000,620000,5650000],'source_ids':['S07'],
        'quality':'coarse historical-map abstraction; mixed sheet dates, not exact 1989 reconstruction',
        'terrain_rows':[''.join(map(str,row)) for row in terrain],
        'infra_rows':[''.join(map(str,row)) for row in infra],
        'places':[{'name':n,'x':x,'y':y,'evidence_kind':'model','source_ids':['S07']} for n,x,y in places],
        'roads':roads,'rivers':rivers,'border_split_x':border,
        'blocked_river_edges':blocked,'bridge_edges':bridges,
        'blue_supply_sources':[[0,8]],'red_supply_sources':[[49,3]],
        'hubs':[[13,8],[13,28],[35,11],[39,24]],'airports':[],
        'notes':['Road traces and river crossing edges are generalized to 2 km. Bridge cells are model crossings, not a verified historical bridge census.','Sheet-specific edition dates and minor roads are unresolved; no post-1989 roads intentionally introduced.']
    }
    write(SCENE/'historical_map.json', map_data)
    write(SCENE/'occupation_state.json', {'width':50,'height':30,'rows':control})
    all_units = {}
    for path in (ROOT/'Scripts/Data/Units').glob('*.json'):
        if path.name != 'fulda_1989_units.json': all_units.update(json.loads(path.read_text(encoding='utf-8-sig')))
    units = {}
    def unit(uid, base, name, attack, defense, hp, cost, caps=None, arty=None):
        u = copy.deepcopy(all_units[base]); u['name']=name
        u['combat_stats']={'max_hp':hp,'attack':attack,'defense':defense}
        u['system_vars']['cost']=cost
        if caps is not None: u['tactical_tags']['capabilities']=caps
        if arty is not None: u['arty_area']=arty
        u['calibration']={'evidence_kind':'model','version':'fulda-v1','as_of':'1989-07-01 scenario assumption',
            'source_ids':['S02'] if uid.startswith(('fg_m1','fg_m3','fg_scout')) else ['S08'],
            'confidence':'equipment-family reference; all combat parameters are authorized model assumptions',
            'quantity_semantics':'one vehicle or one dismounted squad; HP is effectiveness, not vehicle count'}
        units[uid]=u
    unit('fg_m1a1','us_m1a1_abrams','M1A1（1989 场景）',3,2.5,12,300)
    unit('fg_m1a1_cp','us_m1a1_abrams_cp','M1A1 指挥车（1989 场景）',3,2.5,12,300)
    unit('fg_m3a1','us_m2a2_bradley','M3A1 骑兵战车',1.5,2,6,180,['Armor','HeavyAntiTank','Recon'])
    unit('fg_m3a1_cp','us_m2a2_bradley','M3A1 指挥侦察车',1.5,2,6,180,['Armor','HeavyAntiTank','Recon','Command'])
    unit('fg_scout','us_mech_rifles','下车侦察小组（抽象班）',.5,.8,6,50,['AntiTank','Recon'])
    unit('fg_t80b','sov_t72b','T-80B（装备型号假设）',2.8,2.4,10,280)
    unit('fg_t80b_cp','sov_t72b','T-80B 指挥车',2.8,2.4,10,280,['HeavyArmor','HeavyAntiTank','Command'])
    unit('fg_bmp1','sov_bmp1','BMP-1',1,1.2,4,120)
    unit('fg_bmp1_cp','sov_bmp1','BMP-1 指挥车',1,1.2,4,120,['Armor','AntiTank','Command'])
    unit('fg_rifles','sov_motostrelkovy','摩托步兵班',1,1,9,50,['AntiTank'])
    unit('fg_brm1k','sov_bmp1','BRM-1K 侦察指挥车（配属假设）',1,1.2,4,120,['Armor','AntiTank','Recon','Command'])
    unit('fg_red_scout','sov_motostrelkovy','团侦察小组（配属假设）',.5,.8,6,50,['AntiTank','Recon'])
    unit('fg_m109a2','us_m109a2','M109A2（常规弹）',2,2,4,180,arty=9)
    unit('fg_2s1','sov_2s1_gvozdika','2S1（常规弹）',1.5,1.5,4,150,arty=7)
    write(ROOT/'Scripts/Data/Units/fulda_1989_units.json', units)
    templates={}
    def platoon(uid,n):
        return {'type':'standard','units':{f'u{i+1}':{'unit_id':uid,'max_hp':units[uid]['combat_stats']['max_hp']} for i in range(n)}}
    def template(tid,name,groups,role='main',vision=2):
        templates[tid]={'name':name,'role':role,'vision_range':vision,'companies':{
            cid:{'name':cid,'platoons':{pid:platoon(uid,n) for pid,uid,n in ps}} for cid,ps in groups},
            'provenance':{'evidence_kind':'model','source_ids':['S02'] if tid.startswith(('fg_cav','fg_m109')) else ['S08'],
                'as_of':'1989-07-01 scenario assumption','confidence':'doctrinal abstraction, not actual daily strength',
                'quantity_basis':'source-informed authorized TOE assumption; not daily actual strength'}}
    troop=[('Tank1','fg_m1a1',4),('Tank2','fg_m1a1',4),('HQ_Tank','fg_m1a1_cp',1),('Scout1','fg_m3a1',6),('Scout2','fg_m3a1',6),('Dismount','fg_scout',6)]
    template('fg_cav_squadron','第11 ACR 骑兵中队（主战营）',
        [('HQ',[('ScoutHQ','fg_m3a1_cp',2)])]+[(c,troop) for c in ['Troop1','Troop2','Troop3']]+[('TankCompany',[('TankHQ','fg_m1a1_cp',2),('TankPlatoons','fg_m1a1',12)])],vision=4)
    template('fg_m109_battery','骑兵中队属 M109 炮兵连（分离支援）',[('Battery',[('Guns','fg_m109a2',6)])],role='artillery')
    template('fg_tank_bn','苏军坦克模拟营（31 辆）',[('HQ',[('Command','fg_t80b_cp',1)])]+[(c,[('Tanks','fg_t80b',10)]) for c in ['Company1','Company2','Company3']])
    template('fg_mrb','苏军 BMP 摩步模拟营',[('HQ',[('Command','fg_bmp1_cp',1),('Rifles','fg_rifles',1)])]+[(c,[('Vehicles','fg_bmp1',10),('Rifles','fg_rifles',9)]) for c in ['Company1','Company2','Company3']])
    template('fg_2s1_bn','团属 2S1 炮兵模拟营',[('Battery'+str(i),[('Guns','fg_2s1',6)]) for i in range(1,4)],role='artillery')
    template('fg_regimental_recon','团侦察连（营级支援棋子）',[('ReconCompany',[('Vehicles','fg_brm1k',3),('Scouts','fg_red_scout',3)])],role='support',vision=4)
    write(ROOT/'Scripts/Data/Templates/fulda_1989_templates.json', templates)
    def entry(uid,tid,x,y,name,parent,sources,role_note):
        return {'instance_id':uid,'template_id':tid,'x':x,'y':y,'name':name,'parent':parent,
                'provenance':{'source_ids':sources,'evidence_kind':'assumed','as_of':'1989-07-01 scenario assumption','confidence':'formation sourced; strength and combat deployment modeled','notes':role_note}}
    blue=[entry('11ACR_1Sqdn','fg_cav_squadron',19,16,'11 ACR 第1中队','11ACR',['S01','S02'],'Geisa direction forward screen'),
          entry('11ACR_3Sqdn','fg_cav_squadron',22,4,'11 ACR 第3中队','11ACR',['S01','S02'],'northern corridor forward screen'),
          entry('11ACR_1Sqdn_Battery','fg_m109_battery',17,17,'第1中队属炮兵连','11ACR_1Sqdn',['S02'],'detached from squadron; not duplicated'),
          entry('11ACR_3Sqdn_Battery','fg_m109_battery',19,5,'第3中队属炮兵连','11ACR_3Sqdn',['S02'],'detached from squadron; not duplicated')]
    red=[]
    for i,(x,y) in enumerate([(24,15),(25,12),(27,14)],1):
        red.append(entry(f'39GMRD_117GMRR_ModelMRB{i}','fg_mrb',x,y,f'117近卫摩步团 第{i}模拟营','39GMRD_117GMRR',['S05','S08'],'leading echelon; modeled battalion numbering'))
    red.append(entry('39GMRD_117GMRR_ModelTB','fg_tank_bn',28,15,'117近卫摩步团 坦克模拟营','39GMRD_117GMRR',['S05','S08'],'31-tank variant selected as assumption'))
    red.append(entry('39GMRD_117GMRR_ModelArty','fg_2s1_bn',29,13,'117近卫摩步团 炮兵模拟营','39GMRD_117GMRR',['S05','S08'],'18-gun doctrinal model'))
    for i,(x,y) in enumerate([(36,3),(38,4),(40,3)],1):
        red.append(entry(f'79GTD_17GTR_ModelTB{i}','fg_tank_bn',x,y,f'17近卫坦克团 第{i}坦克模拟营','79GTD_17GTR',['S04','S08'],'following echelon already on eastern rear road; no timed spawn'))
    red.append(entry('79GTD_17GTR_ModelMRB','fg_mrb',40,5,'17近卫坦克团 摩步模拟营','79GTD_17GTR',['S04','S08'],'BMP-1-only simplified mix; not CFE actual count'))
    red.append(entry('79GTD_17GTR_ModelArty','fg_2s1_bn',42,3,'17近卫坦克团 炮兵模拟营','79GTD_17GTR',['S04','S08'],'18-gun doctrinal model'))
    red.append(entry('39GMRD_117GMRR_ModelRecon','fg_regimental_recon',26,13,'117近卫摩步团 侦察连','39GMRD_117GMRR',['S08'],'regimental reconnaissance attachment; three vehicles modeled, not historical count'))
    red.append(entry('79GTD_17GTR_ModelRecon','fg_regimental_recon',35,3,'17近卫坦克团 侦察连','79GTD_17GTR',['S08'],'regimental reconnaissance attachment; three vehicles modeled, not historical count'))
    write(SCENE/'oob_blue.json',{'faction_blue':blue})
    write(SCENE/'oob_red.json',{'faction_red':red})
    # Reviewable standalone map, including deployment and river abstraction.
    preview=Image.new('RGB',(1000,600)); draw=ImageDraw.Draw(preview)
    colors=['#d8d1a1','#57835b','#bcb0a4','#8a7975']
    for y in range(30):
        for x in range(50):
            draw.rectangle((x*20,y*20,x*20+19,y*20+19),fill=colors[terrain[y][x]],outline='#89917f')
    for road in roads: draw.line([(x*20+10,y*20+10) for x,y in road['points']], fill='#e3b751',width=4 if road['kind']==2 else 2)
    for river in rivers: draw.line([(x*20+10,y*20+10) for x,y in river['points']],fill='#328cba',width=3)
    draw.line([(border[y]*20,y*20+10) for y in range(30)],fill='#202020',width=3)
    for n,x,y in places: draw.text((x*20+5,y*20),n,fill='black')
    for rows,color in [(blue,'#1d50c9'),(red,'#bd2020')]:
        for e in rows:
            x,y=e['x']*20+10,e['y']*20+10
            draw.ellipse((x-5,y-5,x+5,y+5),fill=color)
    preview.save(REF/'historical_grid_preview.png')

if __name__=='__main__': main()
