const fs = require('fs');
const path = require('path');
const util = require('util');
const readFile = util.promisify(fs.readFile);
const writeFile = util.promisify(fs.writeFile);

const Ajv = require('ajv');
const ajv = new Ajv();
const addFormats = require('ajv-formats');
addFormats(ajv);

const root = path.join(__dirname, 'manifests');

const schema = require('./manifest.schema.json');
const validate = ajv.compile(schema);

async function scanDir(dir) {
    let manifests = [];
    const report = {
        failed: 0,
        successful: 0,
        total: 0,
        invalid: 0
    };
    const files = await fs.promises.readdir(dir);
    for( const file of files ) {
        const fullPath = path.join( dir, file );
        
        if(fs.lstatSync(fullPath).isDirectory()) {
            const {manifests: jsons, report: report2} = await scanDir(fullPath);
            report.total += report2.total;
            report.failed += report2.failed;
            report.invalid += report2.invalid;
            report.successful += report2.successful;
            manifests = [...manifests, ...jsons];
        } else {
            try {
                console.log('\x1b[0m', 'Found manifest in ' + fullPath);
                const data = await readFile(fullPath);
                const json = JSON.parse(data.toString('utf8').replace(/^\uFEFF/, ''));
                const valid = validate(json);
                report.total++;
                if(!valid) {
                    report.failed++;
                    console.log('\x1b[31m','INVALID MANIFEST! ' + fullPath);
                    console.log('\x1b[31m',JSON.stringify(validate.errors));
                } else {
                    report.successful++;
                    console.log("\x1b[32m",'Manifest valid at ' + fullPath);
                    manifests.push(json);
                }    
            } catch(e) {
                console.log('\x1b[31m', 'INVALID MANIFEST! ' + e.message);
                report.invalid++;
            }        
        }        
    }
    return {manifests, report};
}

(async ()=>{
    console.log('Scanning for manifests in ' + root);
    const {manifests, report} = await scanDir(root);
    const targetDir = __dirname + '/Plugins';
    if (!fs.existsSync(targetDir)){
        fs.mkdirSync(targetDir);
    }
    await writeFile(targetDir + '/manifests.json', JSON.stringify(manifests), function(err, result) {
        if(err) console.log('\x1b[31m','error', err);
      });
      console.log('\x1b[0m',`done - total: ${report.total} total, ${report.successful} successful, ${report.failed} failed, ${report.invalid} invalid`);
})();


