"""Reuse immutable APKs only when their application and native tests are unchanged."""
import json
import os
from pathlib import Path
import re
import subprocess

repository=os.environ['GITHUB_REPOSITORY']
run=os.environ['MOJI_REUSE_RUN']
head=os.environ['GITHUB_SHA']
if not run.isdigit():raise RuntimeError('Invalid build run ID')
def api(path):return json.loads(subprocess.check_output(['gh','api',path],text=True))
base=api(f'repos/{repository}/actions/runs/{run}')
if base['head_repository']['full_name']!=repository or base['head_branch']!='main' or base['path']!='.github/workflows/android.yml':
    raise RuntimeError('Only this repository’s main Android build can be reused')
sha=base['head_sha']
if not re.fullmatch('[0-9a-f]{40}',sha):raise RuntimeError('Invalid source commit')
jobs=api(f'repos/{repository}/actions/runs/{run}/jobs')['jobs']
passed={step['name'] for job in jobs for step in job['steps'] if step['conclusion']=='success'}
required={'Run npm test --prefix ../editor -- --workers=2',
 'Run ./gradlew :app:testDebugUnitTest :app:assembleDebug :app:assembleRelease :app:assembleDebugAndroidTest --no-daemon',
 'Verify fixed Android publisher certificate','Run native phone, tablet and desktop-window UI checks'}
if not required.issubset(passed):raise RuntimeError('Build, unit tests, signature and all native UI checks must have passed')
subprocess.run(['git','diff','--exit-code',sha,head,'--','android/app','android/build.gradle.kts',
 'android/settings.gradle.kts','android/gradle.properties','android/gradle','android/gradlew','android/gradlew.bat','editor','assets'],check=True)
folder=Path('android/app/build');folder.mkdir(parents=True,exist_ok=True)
(folder/'REUSED-BUILD.json').write_text(json.dumps({'binary_source_commit':sha,
 'binary_workflow_run':f'https://github.com/{repository}/actions/runs/{run}',
 'verification_commit':head},indent=2)+'\n')
print('Application, build settings, shared editor and native UI tests match the verified APK build.')
