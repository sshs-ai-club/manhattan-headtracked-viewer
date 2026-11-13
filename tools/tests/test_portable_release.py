import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
import zipfile

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import package_portable_windows as portable
from build_portable_tracker import PYTHON_PATHS, CMD_ENTRY, MODEL_LOADING_PATCH, extract_checked, portable_tracker_source


class PortableReleaseTests(unittest.TestCase):
    def setUp(self):
        scratch=portable.ROOT/'.cache/tests'; scratch.mkdir(parents=True,exist_ok=True)
        self.temp=tempfile.TemporaryDirectory(dir=scratch); self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name); self.stage=self.root/'stage'; self.stage.mkdir()
        (self.root/'tools').mkdir()
        self.viewer='1'*40; self.technology='2'*40; self.revision='3'*40
        self.model=b'fixture model'
        self.model_hash=hashlib.sha256(self.model).hexdigest()
        (self.root/'tools/technology-version.json').write_text(json.dumps({'revision':self.technology,'pythonVersion':'3.13.5','faceModelSha256':self.model_hash}))
        self.patch=mock.patch.object(portable,'ROOT',self.root); self.patch.start(); self.addCleanup(self.patch.stop)
        self.archive=self.root/'base.zip'
        self.base_data={'ManhattanViewer.exe':b'fixture player','README-Windows.md':b'old guide'}
        self.write_base()
        for name in portable.SHIPPING_TOOLS: self.write(name,b'fixture launcher')
        self.write('Run-HeadTrackedDemo.cmd',CMD_ENTRY)
        self.write('README-Windows.md',b'English Windows guide')
        self.write('.runtime/tracker/face_landmarker.task',self.model)
        tracker_source = b'fixture tracker'
        self.write('.runtime/tracker/tracker.py',tracker_source)
        self.write('.runtime/tracker/portable-tracker.json',json.dumps({'technologyRevision':self.technology,'pythonVersion':'3.13.5',
            'trackerCompatibilityPatch':MODEL_LOADING_PATCH,'portableTrackerSha256':hashlib.sha256(tracker_source).hexdigest()}).encode())
        self.write('.runtime/tracker/.venv/Scripts/python313._pth',PYTHON_PATHS.encode())
        self.write('.runtime/tracker/.venv/Scripts/python.exe',b'fixture interpreter')
        for name in ('LICENSE-Python.txt','LICENSE-MediaPipe.txt','README.md'): self.write('Notices/PortableTracker/'+name,b'original notice')

    def write(self,name,data):
        path=self.stage/name; path.parent.mkdir(parents=True,exist_ok=True); path.write_bytes(data); return path

    def write_base(self,wrong_digest=False):
        records=[{'path':name,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()} for name,data in self.base_data.items()]
        if wrong_digest: records[0]['sha256']='0'*64
        receipt={'buildIdentity':{'viewerRevision':self.viewer,'technologyRevision':self.technology,'dirtyWorkingTree':False},'files':records}
        with zipfile.ZipFile(self.archive,'w') as z:
            for name,data in self.base_data.items(): z.writestr(name,data)
            z.writestr('INTEGRITY.json',json.dumps(receipt))
        (self.root/'tools/portable-player.json').write_text(json.dumps({'sha256':portable.base.sha256_file(self.archive),'viewerRevision':self.viewer}))

    def package(self): return portable.package(self.archive,self.stage,self.root/'release.zip',self.revision)

    def test_both_modes_ship_and_every_receipt_hash_matches(self):
        result=self.package()
        with zipfile.ZipFile(result['zip']) as z:
            receipt=json.loads(z.read('INTEGRITY.json'))
            self.assertEqual(receipt['distributionRevision'],self.revision)
            self.assertEqual(receipt['buildIdentity']['viewerRevision'],self.viewer)
            for record in receipt['files']:
                self.assertEqual(hashlib.sha256(z.read(record['path'])).hexdigest(),record['sha256'])
            self.assertEqual(z.read('README-Windows.md'),b'English Windows guide')
            self.assertIn('Run-HeadTrackedDemo.cmd',z.namelist())
            self.assertIn('.runtime/tracker/face_landmarker.task',z.namelist())

    def test_personal_state_and_generated_caches_are_excluded(self):
        for name in ('.runtime/tracker/physical-calibration.json','.runtime/tracker/tracker_camera_preference.json',
                     '.runtime/tracker/__pycache__/tracker.pyc','.runtime/tracker/viewer-preferences.json'):
            self.write(name,b'private fixture')
        with zipfile.ZipFile(self.package()['zip']) as z:
            self.assertFalse(any('private fixture' in z.read(name).decode(errors='ignore') for name in z.namelist()))

    def test_unlisted_staging_content_is_rejected(self):
        self.write('source-archive.zip',b'unlisted')
        with self.assertRaisesRegex(ValueError,'Unlisted'): self.package()

    def test_changed_model_and_machine_specific_python_paths_are_rejected(self):
        self.write('.runtime/tracker/face_landmarker.task',b'wrong')
        with self.assertRaisesRegex(ValueError,'model digest'): self.package()
        self.write('.runtime/tracker/face_landmarker.task',self.model)
        self.write('.runtime/tracker/.venv/Scripts/python313._pth',b'C:\\Python313\nimport site\n')
        with self.assertRaisesRegex(ValueError,'isolated and relative'): self.package()

    def test_base_archive_change_and_internal_hash_mismatch_are_rejected(self):
        self.archive.write_bytes(self.archive.read_bytes()+b'changed')
        with self.assertRaisesRegex(ValueError,'checksum'): self.package()
        self.write_base(wrong_digest=True)
        with self.assertRaisesRegex(ValueError,'Shipping input differs'): self.package()
        self.assertFalse((self.root/'release.zip').exists())

    def test_different_base_identity_is_rejected(self):
        pin=json.loads((self.root/'tools/portable-player.json').read_text()); pin['viewerRevision']='9'*40
        (self.root/'tools/portable-player.json').write_text(json.dumps(pin))
        with self.assertRaisesRegex(ValueError,'identity'): self.package()

    def test_cmd_that_loses_failure_status_or_depends_on_path_is_rejected(self):
        self.write('Run-HeadTrackedDemo.cmd',b'@echo off\npowershell.exe -File tools/run_demo.ps1\nif errorlevel 1 pause\n')
        with self.assertRaisesRegex(ValueError,'preserve its exit status'): self.package()

    def test_download_archive_traversal_and_case_alias_are_rejected(self):
        for names in (('../outside.txt',),('python.exe','PYTHON.exe')):
            archive=self.root/'unsafe.zip'
            with zipfile.ZipFile(archive,'w') as z:
                for name in names: z.writestr(name,b'fixture')
            with self.assertRaises(ValueError): extract_checked(archive,self.root/'unpacked')
        self.assertFalse((self.root/'outside.txt').exists())

    def test_unicode_model_loading_and_upstream_drift_guard(self):
        model = self.root/'모델 & 경로!'/ 'face_landmarker.task'
        model.parent.mkdir()
        model.write_bytes(self.model)
        source = b'options = mp.tasks.BaseOptions(model_asset_path=str(model))'
        from types import SimpleNamespace
        captured = []
        def base_options(**kwargs):
            captured.append(kwargs)
        exec(portable_tracker_source(source), {'model':model,'mp':SimpleNamespace(tasks=SimpleNamespace(BaseOptions=base_options))})
        self.assertEqual(captured, [{'model_asset_buffer':self.model}])
        for changed in (b'changed upstream', source+source):
            with self.assertRaisesRegex(ValueError,'expression differs'):
                portable_tracker_source(changed)

    def explicit_pin(self):
        path=self.root/'current-player.json'
        path.write_text((self.root/'tools/portable-player.json').read_text())
        return path

    def cli_args(self, pin=None, version=None):
        args=['--base-player-zip',str(self.archive),'--runtime-stage',str(self.stage),
              '--output',str(self.root/'release.zip')]
        if pin is not None: args.extend(['--player-pin',str(pin)])
        if version is not None: args.extend(['--distribution-version',version])
        return args

    def test_explicit_pin_preserves_identity_without_changing_default(self):
        pin=portable.load_player_pin(self.explicit_pin())
        default=self.root/'tools/portable-player.json'
        default.write_text(json.dumps({'sha256':'0'*64,'viewerRevision':'9'*40}))
        unchanged=default.read_bytes()
        result=portable.package(self.archive,self.stage,self.root/'release.zip',self.revision,
                                'v0.2.0-preview.1',player_pin=pin)
        with zipfile.ZipFile(self.archive) as original, zipfile.ZipFile(result['zip']) as output:
            before=json.loads(original.read('INTEGRITY.json'))
            after=json.loads(output.read('INTEGRITY.json'))
            self.assertEqual(after['buildIdentity'],before['buildIdentity'])
            self.assertEqual(after['distributionVersion'],'v0.2.0-preview.1')
            self.assertEqual(after['baseArchiveSha256'],pin['sha256'])
        self.assertEqual(default.read_bytes(),unchanged)

    def test_validated_player_round_trip_accepts_powershell_bom_pin(self):
        from test_package_windows import WindowsPackageTests
        player=WindowsPackageTests('test_zip_is_reproducible_and_receipt_hashes_exact_allowed_payload')
        self.addCleanup(player.doCleanups)
        player.setUp()
        built=player.package()
        pin_path=self.root/'validated-player.json'
        pin_path.write_text(json.dumps({'sha256':built['zipSha256'],
                                       'viewerRevision':player.identity['viewerRevision']}),encoding='utf-8-sig')
        self.assertTrue(pin_path.read_bytes().startswith(b'\xef\xbb\xbf'))
        pin=portable.load_player_pin(pin_path)
        result=portable.package(Path(built['zip']),self.stage,self.root/'release.zip',self.revision,
                                'v0.2.0-preview.1',player_pin=pin)
        with zipfile.ZipFile(built['zip']) as original, zipfile.ZipFile(result['zip']) as output:
            receipt=json.loads(output.read('INTEGRITY.json'))
            self.assertEqual(receipt['buildIdentity'],player.identity)
            self.assertEqual(receipt['distributionVersion'],'v0.2.0-preview.1')
            self.assertEqual(receipt['baseArchiveSha256'],built['zipSha256'])
            resources='ManhattanViewer_Data/resources.assets'
            self.assertEqual(output.read(resources),original.read(resources))
            for record in receipt['files']:
                data=output.read(record['path'])
                self.assertEqual(len(data),record['bytes'])
                self.assertEqual(hashlib.sha256(data).hexdigest(),record['sha256'])

    def test_invalid_pin_values_and_duplicate_keys_are_rejected(self):
        good=portable.load_player_pin(self.explicit_pin())
        invalid=[None,[],{},dict(good,sha256=True),dict(good,sha256='A'*64),
                 dict(good,viewerRevision='main'),dict(good,viewerRevision='1'*39)]
        for value in invalid:
            with self.subTest(value=value), self.assertRaises(ValueError):
                portable.validate_player_pin(value)
        path=self.root/'duplicate.json'
        path.write_text('{"sha256":"'+good['sha256']+'","sha256":"'+good['sha256']+'","viewerRevision":"'+self.viewer+'"}')
        with self.assertRaisesRegex(ValueError,'Duplicate JSON key'): portable.load_player_pin(path)
        with self.assertRaises(FileNotFoundError): portable.load_player_pin(self.root/'missing.json')

    def test_explicit_pin_requires_valid_explicit_distribution_version(self):
        pin=portable.load_player_pin(self.explicit_pin())
        for version in (None,'','../release','v1\n','x'*65,True):
            with self.subTest(version=version), self.assertRaises(ValueError):
                portable.package(self.archive,self.stage,self.root/'release.zip',self.revision,
                                 version,player_pin=pin)
        self.assertFalse((self.root/'release.zip').exists())

    def test_explicit_pin_keeps_archive_and_identity_guards(self):
        pin=portable.load_player_pin(self.explicit_pin())
        for changed,message in ((dict(pin,sha256='0'*64),'checksum'),
                                (dict(pin,viewerRevision='9'*40),'identity')):
            with self.subTest(message=message), self.assertRaisesRegex(ValueError,message):
                portable.package(self.archive,self.stage,self.root/'release.zip',self.revision,
                                 'v0.2.0',player_pin=changed)

    def test_cli_uses_one_pin_snapshot_for_source_guard_and_package(self):
        path=self.explicit_pin()
        def changed_after_guard(*args,**kwargs):
            path.write_text('{}')
        with mock.patch.object(portable.subprocess,'check_output',side_effect=[self.revision,'']), \
             mock.patch.object(portable.subprocess,'run',side_effect=changed_after_guard) as compare, \
             mock.patch('builtins.print'):
            portable.main(self.cli_args(path,'v0.2.0'))
        compare.assert_called_once_with(['git','-C',str(self.root),'diff','--exit-code',self.viewer,self.revision,
                                        '--','ManhattanViewer','assets/source-manifest.json','tools/technology-version.json'],check=True)
        with zipfile.ZipFile(self.root/'release.zip') as output:
            self.assertEqual(json.loads(output.read('INTEGRITY.json'))['distributionVersion'],'v0.2.0')

    def test_cli_legacy_defaults_are_preserved(self):
        with mock.patch.object(portable.subprocess,'check_output',side_effect=[self.revision,'']), \
             mock.patch.object(portable.subprocess,'run'), mock.patch('builtins.print'):
            portable.main(self.cli_args())
        with zipfile.ZipFile(self.root/'release.zip') as output:
            self.assertEqual(json.loads(output.read('INTEGRITY.json'))['distributionVersion'],'v0.1.1')

    def test_cli_rejects_dirty_source_or_source_drift_before_packaging(self):
        path=self.explicit_pin()
        for status,error in ((' M changed.py',None),('',subprocess.CalledProcessError(1,['git','diff']))):
            with self.subTest(status=status), \
                 mock.patch.object(portable.subprocess,'check_output',side_effect=[self.revision,status]), \
                 mock.patch.object(portable.subprocess,'run',side_effect=error), \
                 mock.patch.object(portable,'package') as package, \
                 mock.patch('sys.stderr'), self.assertRaises(SystemExit) as stopped:
                portable.main(self.cli_args(path,'v0.2.0'))
            self.assertEqual(stopped.exception.code,1)
            package.assert_not_called()

    def test_cli_rejects_missing_pin_or_version_before_git(self):
        path=self.explicit_pin()
        for pin,version in ((path,None),(self.root/'missing.json','v0.2.0'),(path,'bad/version')):
            with self.subTest(pin=pin,version=version), \
                 mock.patch.object(portable.subprocess,'check_output') as git, \
                 mock.patch('sys.stderr'), self.assertRaises(SystemExit) as stopped:
                portable.main(self.cli_args(pin,version))
            self.assertEqual(stopped.exception.code,1)
            git.assert_not_called()

    @unittest.skipUnless(shutil.which('git'),'Git is required for the source-drift integration check')
    def test_real_git_source_guard_rejects_changed_unity_source(self):
        def git(*args):
            return subprocess.check_output(['git','-C',str(self.root),*args],stderr=subprocess.STDOUT,text=True).strip()
        git('init','--quiet')
        source=self.root/'ManhattanViewer/fixture.txt'; source.parent.mkdir(); source.write_text('original')
        git('add','.')
        git('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','-qm','Fixture baseline')
        original=git('rev-parse','HEAD')
        source.write_text('changed')
        git('add','.')
        git('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','-qm','Changed source')
        # A tracked tooling-only pin is allowed, but cannot authorize changed player sources.
        path=self.root/'tools/portable-player.json'
        pin=json.loads(path.read_text()); pin['viewerRevision']=original; path.write_text(json.dumps(pin))
        git('add','.')
        git('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','-qm','Pin baseline')
        with mock.patch.object(portable,'package') as package, mock.patch('sys.stderr'), self.assertRaises(SystemExit):
            portable.main(self.cli_args(path,'v0.2.0'))
        package.assert_not_called()


if __name__=='__main__': unittest.main()
