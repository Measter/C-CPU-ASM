; Test C-CPU Assembly

seti r3, outLoc		; Output location.

seti r2, 5			; Counter.
seti.l r1, hA0
seti r0, 1

:loopStart
	str r1, r3

	sub r2, r0		; Counter
	add r3, r0		; Mem location.

	cmp r2, r4
	jgt loopStart

#at h80
:outLoc