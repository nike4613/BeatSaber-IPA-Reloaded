import os
import shutil

def toFileList(text):
	lines = text.splitlines()
	files = []
	stack = []
	def push(val):
		pre = ''
		if len(stack) > 0:
			pre = stack[-1]
		stack.append(pre + val)
	def pop():
		return stack.pop()
	def replace(val):
		val2 = pop()
		push(val)
		return val2
	
	for line in lines:
		spl = line.split('"');
		spath = spl[-1]
		semis = len(spl[:-1])
		
		if spath.startswith('::'):
			spl = spath.split(' ')
			cmd = spl[0][2:]
			if cmd == 'from': # basically just import
				content = ''
				with open(' '.join(spl[1:]),'r') as f:
					content = f.read()
				spath = content
			elif cmd == 'prompt':
				spath = input(' '.join(spl[1:]))
			else:
				spath = ''
				print("No such command", cmd)
		
		if semis > (len(stack)-1):
			push(spath)
		elif semis == (len(stack)-1):
			files.append(replace(spath))
		elif semis < (len(stack)-1):
			files.append(pop())
			while semis < (len(stack)):
				pop()
			push(spath)
	files.append(pop())
	return files
		
if __name__ == '__main__':
	file = ''
	with open("refs.txt","r") as f:
		file = f.read()

	refs = toFileList(file)

	tgtDir = "target/"
	shutil.rmtree(tgtDir, ignore_errors=True)
	if not os.path.exists(tgtDir):
		os.makedirs(tgtDir)
	
	for filename in refs:
		if (os.path.isfile(filename)):
			print("Copying",filename)
			shutil.copy(filename, tgtDir)