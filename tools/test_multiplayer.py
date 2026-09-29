#!/usr/bin/env python3
"""Real, bounded ENet host/client scenarios. No router/firewall changes."""
import argparse,json,socket,subprocess,time
from pathlib import Path
from test_control_files import replace_control_file

ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--godot');p.add_argument('--executable');p.add_argument('--output',type=Path,default=ROOT/'artifacts/multiplayer-checks')
a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
if a.executable: base=[str(Path(a.executable).resolve())]
elif a.godot: base=[a.godot,'--path',str(ROOT/'src/LuckerParty.Godot')]
else: raise RuntimeError('Provide --godot or --executable')
with socket.socket(socket.AF_INET,socket.SOCK_DGRAM) as s:
    s.bind(('127.0.0.1',0));port=s.getsockname()[1]
processes=[];reports=[];proxy=None
class Peer:
    def __init__(self,label,connection_port=None):
        self.port=connection_port or port
        self.control=a.output/(label+'-control.json');self.probe=a.output/(label+'-probe.json');self.revision=0
        self.probe.unlink(missing_ok=True);self.control.unlink(missing_ok=True)
        self.log=a.output/(label+'.log');self.file=self.log.open('w',encoding='utf-8')
        self.process=subprocess.Popen([*base,'--headless','--','--network-camera-check','--control-file',str(self.control.resolve()),'--probe-file',str(self.probe.resolve()),'--exit-after','150'],stdout=self.file,stderr=subprocess.STDOUT)
        processes.append(self)
    def send(self,**values):
        self.revision+=1;values.update(revision=self.revision,port=self.port)
        temp=self.control.with_suffix('.tmp');temp.write_text(json.dumps(values),encoding='utf-8');replace_control_file(temp,self.control)
    def read(self):
        try:return json.loads(self.probe.read_text(encoding='utf-8'))
        except (FileNotFoundError,json.JSONDecodeError,PermissionError):return {}
    def stop(self):
        if self.process.poll() is None:
            self.send(action='quit')
            try:self.process.wait(timeout=4)
            except subprocess.TimeoutExpired:self.process.terminate();self.process.wait(timeout=4)
        self.file.flush()

def wait(description,condition,timeout=15):
    end=time.monotonic()+timeout
    while time.monotonic()<end:
        if condition():
            print('NETWORK_CHECK_PASS: '+description,flush=True);reports.append(description);return
        failed=[peer for peer in processes if peer.process.poll() not in (None,0)]
        if failed:raise RuntimeError(description+'; peer crashed: '+str([p.log for p in failed]))
        time.sleep(.05)
    raise RuntimeError(description+' timed out\n'+json.dumps({p.log.stem:p.read() for p in processes},indent=2))

def player(peer,name):return next((v for v in peer.read().get('players',[]) if v['name']==name),{})
def count(peer,n):return len(peer.read().get('players',[]))==n
try:
    host=Peer('host');host.send(action='host',name='Host')
    wait('Listen host opens the arena',lambda:host.read().get('isServer') and count(host,1))
    blocked=Peer('occupied');blocked.send(action='host',name='Blocked')
    wait('Occupied port reports a recoverable host error',lambda:blocked.read().get('status')=='Host failed')
    blocked.stop()
    client=Peer('client');client.send(action='join',name='Alice')
    wait('Client and host agree on two members',lambda:count(host,2) and count(client,2) and player(client,'Alice').get('local'))
    wait('Initial client prediction settles',lambda:player(client,'Alice').get('pending',100)<8)
    start=player(host,'Alice')['z']
    host.send(action="pause")
    client.send(y=-1,rawCamera=True)
    wait('Client input moves its capsule on the authoritative host',lambda:player(host,'Alice').get('z',100)<start-5,timeout=4)
    raw=client.read()
    assert raw['cameraStationary']/max(raw['cameraFrames'],1)>.3,'Raw camera check did not expose physics stepping'
    client.send(reset=True)
    wait('Raw movement resets before camera comparison',lambda:abs(player(host,'Alice').get('z',0)-start)<.1)
    client.send(y=-1)
    wait('Interpolated camera moves at 240 FPS',lambda:player(host,'Alice').get('z',100)<start-5,timeout=4)
    smooth=client.read()
    assert smooth['cameraStationary']/max(smooth['cameraFrames'],1)<.2,'Network camera visibly stalls between physics ticks'
    host.send(action='resume')
    reports.append('Host menu leaves remote players and physics running')
    print('NETWORK_CAMERA_PASS: raw='+str(raw['cameraStationary'])+'/'+str(raw['cameraFrames'])+' smooth='+str(smooth['cameraStationary'])+'/'+str(smooth['cameraFrames']),flush=True)
    client.send()
    wait('Prediction and server position converge after stopping',lambda:abs(player(host,'Alice').get('z',100)-player(client,'Alice').get('z',0))<.25)
    client.send(action='rename',name='Alice Renamed')
    wait('Name change replicates to host and client',lambda:bool(player(host,'Alice Renamed')) and bool(player(client,'Alice Renamed')))
    client.send(action='rename',name='Should Not Replace')
    wait('Rate-limited name change restores the authoritative name in the client',lambda:client.read().get('revision')==client.revision and client.read().get('localName')=='Alice Renamed' and bool(player(host,'Alice Renamed')))
    late=Peer('late');late.send(action='join',name='Observer')
    wait('Late join receives the existing roster',lambda:count(host,3) and count(client,3) and count(late,3) and bool(player(late,'Alice Renamed')))
    # Reset restores the server-assigned spawn, rather than a client-selected transform.
    client.send(reset=True)
    wait('Reset returns the authoritative capsule to its assigned spawn',lambda:abs(player(host,'Alice Renamed').get('z',0)-start)<.1)
    client.send(jump=True)
    wait('Client jump is simulated by the host',lambda:player(host,'Alice Renamed').get('y',0)>.5,timeout=4)
    wait('Client lands on the arena floor',lambda:abs(player(host,'Alice Renamed').get('y',100))<.03)
    client.send(y=-1,sprint=True)
    wait('Server collision stops the capsule at the red box',lambda:-2.8<player(host,'Alice Renamed').get('z',100)<-2.5,timeout=6)
    before=player(host,'Alice Renamed')['z'];time.sleep(.5)
    assert abs(player(host,'Alice Renamed')['z']-before)<.1,'Client escaped server collision'
    client.send(action='bad-input')
    wait('Malformed movement cannot corrupt the host position',lambda:abs(player(host,'Alice Renamed').get('y',100))<.03)
    client.send(action='leave')
    wait('Leaving removes the capsule from every remaining peer',lambda:count(host,2) and count(late,2) and not client.read().get('active',True))
    client.send(action='join',name='Alice Again')
    wait('Same process can rejoin without stale members',lambda:count(host,3) and count(client,3) and count(late,3) and bool(player(host,'Alice Again')))
    from udp_test_proxy import UDPProxy
    proxy=UDPProxy(('127.0.0.1',port))
    delayed=Peer('delayed',proxy.port);delayed.send(action='join',name='Latency')
    wait('Client joins through 80ms RTT, jitter and 5 percent packet loss',lambda:count(host,4) and count(delayed,4),timeout=20)
    start_delayed=player(host,'Latency')['z']
    delayed.send(x=.5,y=-.5,sprint=True)
    wait('Lossy input stream still moves on the authoritative host',lambda:player(host,'Latency').get('z',100)<start_delayed-3,timeout=5)
    lossy=delayed.read()
    assert lossy['cameraStationary']/max(lossy['cameraFrames'],1)<.2,'Lossy camera stalls at snapshot frequency'
    print('LOSSY_CAMERA_PASS: stationary='+str(lossy['cameraStationary'])+'/'+str(lossy['cameraFrames']),flush=True)
    delayed.send()
    wait('Lossy prediction converges to server position after stopping',lambda:abs(player(host,'Latency').get('z',100)-player(delayed,'Latency').get('z',0))<.3,timeout=5)
    assert player(delayed,'Latency')['correction']<1.5,'Prediction had an excessive correction'
    delayed.send(action='leave')
    wait('Lossy peer disconnect clears the roster',lambda:count(host,3),timeout=10)
    delayed.stop();proxy.close();proxy=None
    burst=Peer('burst');burst.send(action='join',name='Burst')
    wait('Input flood test peer joins',lambda:count(host,4) and count(burst,4))
    burst_start=player(host,'Burst')['z'];started=host.read()['elapsed']
    burst.send(action='flood');time.sleep(1.2)
    burst_end=host.read()
    distance=burst_start-player(host,'Burst')['z']
    assert distance<=9*(burst_end['elapsed']-started)+1.8,'Input flood exceeded server-time movement budget'
    wait('Fast input stream cannot exceed server-time movement budget',lambda:distance>0)
    burst.send(action='leave');wait('Input flood peer leaves cleanly',lambda:count(host,3))
    burst.stop()
    crashed=Peer('crashed');crashed.send(action='join',name='Crash Test')
    wait('Unclean disconnect test peer joins',lambda:count(host,4) and count(crashed,4))
    crashed.process.kill();crashed.process.wait(timeout=4)
    # Its nonzero exit is deliberate; exclude it from ordinary crash detection.
    processes.remove(crashed);crashed.file.close()
    wait('Lost or killed client frees its slot on the host',lambda:count(host,3) and count(late,3),timeout=12)
    host.stop()
    wait('Host departure returns clients to the menu',lambda:not client.read().get('active',True) and not late.read().get('active',True),timeout=12)
    client.send(action='join',name='Nobody')
    wait('Unreachable server times out to a usable menu',lambda:not client.read().get('active',True) and any(text in client.read().get('status','') for text in ['timed out','Could not reach']),timeout=12)
    client.send(action='host',name='New Host')
    wait('Client can host after failed join',lambda:client.read().get('isServer') and count(client,1))
    client.send(action='leave')
    wait('Rehosted lobby closes cleanly',lambda:not client.read().get('active',True))
    dedicated=Peer('dedicated');dedicated.send(action='host',name='Server',dedicated=True)
    wait('Dedicated host starts without a fake player',lambda:dedicated.read().get('isServer') and count(dedicated,0))
    wrong=Peer('mismatch');wrong.send(action='join',name='Old Client',protocol=-1)
    wait('Incompatible protocol is rejected with a clear reason',lambda:'different game protocol' in wrong.read().get('status','') and not wrong.read().get('active',True))
    wrong.stop()
    crowd=[]
    for index in range(8):
        peer=Peer('crowd'+str(index));peer.send(action='join',name='Guest '+str(index));crowd.append(peer)
        wait('Dedicated server accepts player '+str(index+1),lambda:count(dedicated,index+1) and count(peer,index+1))
    overflow=Peer('full');overflow.send(action='join',name='Overflow')
    wait('A ninth player cannot exceed the eight-player limit',lambda:count(dedicated,8) and not overflow.read().get('active',True) and 'host' in overflow.read().get('status','').lower(),timeout=12)
    crowd[0].send(action='leave')
    wait('Leaving frees a full server slot',lambda:count(dedicated,7))
    overflow.send(action='join',name='Replacement')
    wait('Replacement can join the freed slot',lambda:count(dedicated,8) and count(overflow,8))
    print('MULTIPLAYER_CHECK_PASS: '+str(len(reports))+' real host/client scenarios',flush=True)
    (a.output/'results.json').write_text(json.dumps({'port':port,'checks':reports,'passed':True},indent=2))
except BaseException:
    (a.output/'failure.json').write_text(json.dumps({p.log.stem:p.read() for p in processes},indent=2))
    raise
finally:
    for peer in processes:peer.stop();peer.file.close()
    if proxy:proxy.close()
