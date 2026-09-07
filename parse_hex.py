import struct

with open("hex.txt", "r") as f:
    hex_str = f.read().replace("-", "")
    
data = bytes.fromhex(hex_str)

def read_int(offset):
    return struct.unpack_from("<I", data, offset)[0]

def read_byte(offset):
    return data[offset]

offset = 4 # count
count = read_int(0)
print(f"Total {count} instruments")

for i in range(count):
    if offset >= len(data): break
    idx = read_int(offset)
    offset += 4
    type_ = read_byte(offset)
    offset += 1
    
    print(f"Found Inst {idx} Type {type_}")
    
    if type_ == 1:
        seqCnt = read_int(offset)
        offset += 4
        offset += seqCnt * 2
        # dpcm - wait, we don't know block version here so we can't easily skip it
        # Just search for "New instrument" or valid strings to skip?
        pass
    else:
        pass
