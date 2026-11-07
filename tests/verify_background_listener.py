from pathlib import Path
r=Path(__file__).resolve().parent.parent
eng=(r/'src/KeyboardEngine.cs').read_text(encoding='utf-8')
ui=(r/'src/MainForm.cs').read_text(encoding='utf-8')
space=eng.split('if (vk == WinApi.VK_SPACE)')[1].split('if (vk == WinApi.VK_BACK)')[0]
callback=eng.split('private IntPtr KeyboardCallback')[1].split('private static bool IsModifier')[0]
checks={
  'Dedicated pumped hook thread': 'new Thread(HookThreadMain)' in eng and 'WinApi.GetMessage(out message' in eng,
  'Callback hook installed from thread': 'InstallHooks();' in eng.split('private void HookThreadMain')[1].split('private void RemoveHooks')[0],
  '20s re-register timer': '}, null, 20000, 20000)' in eng and 'WinApi.WM_KEYMIND_REHOOK' in eng,
  'Manual restart of listener': 'public bool RestartListener()' in eng and '_engine.RestartListener()' in ui,
  'No dictionary lookup while inside hook': '_converter.EvaluatePhysical' not in callback.split('if (vk == WinApi.VK_SPACE)')[0],
  'Auto correction not computed inline with hook': '_owner.BeginInvoke' in space and '_converter.EvaluatePhysical' in space,
  'Space not swallowed': 'return new IntPtr(1)' not in space,
  'Refuses stale correction on subsequent keys': 'generation != Interlocked.CompareExchange(ref _inputGeneration' in eng,
  'Replaces word plus original space': 'Replace(original + " ", corrected + " ", "")' in eng,
  'Native MSG PInvoke message loop': 'struct MSG' in eng and 'GetMessage(out MSG' in eng,
  'Diagnostics show listener recoveries': 'Listener refreshes:' in eng,
  'Build script includes all C# source files': r'"src\KeyboardEngine.cs"' in (r/'Build-KeyMind.cmd').read_text(),
}
for k,v in checks.items(): print(('[PASS]' if v else '[FAIL]'),k)
assert all(checks.values())
print('Static listener checks passed. Windows compilation and cross-app integration remain unverified.')
